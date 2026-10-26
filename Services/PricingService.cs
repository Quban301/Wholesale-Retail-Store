using Microsoft.EntityFrameworkCore;
using WholesaleRetailStore.Data;
using WholesaleRetailStore.Models;

namespace WholesaleRetailStore.Services
{
    /// <summary>
    /// Calculates prices and quotes from the active <see cref="PricingRule"/>s.
    /// </summary>
    /// <remarks>
    /// Rules are matched on customer type and split by whether they have a minimum order amount:
    /// <list type="bullet">
    /// <item><description>No minimum: a per-unit discount. The best (highest) one reduces every unit price.</description></item>
    /// <item><description>With a minimum: a volume discount. Once the subtotal reaches the minimum, the best
    /// qualifying one is taken off the subtotal.</description></item>
    /// </list>
    /// The two stack by design, so a wholesale order over the threshold gets both.
    /// </remarks>
    public class PricingService
    {
        private readonly AppDbContext _context;

        public PricingService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets the active rules for a customer type.
        /// </summary>
        public async Task<List<PricingRule>> GetActiveRulesAsync(CustomerType customerType, CancellationToken ct = default)
        {
            return await _context.PricingRules
                .AsNoTracking()
                .Where(r => r.IsActive && r.CustomerType == customerType)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Gets the unit price a customer type pays for one product, after the per-unit rule only
        /// (volume discounts depend on the whole order, so they are not part of a unit price).
        /// </summary>
        public static decimal CalculateUnitPrice(Product product, IReadOnlyList<PricingRule> rules)
        {
            return ApplyUnitDiscount(product.BasePrice, BestUnitRule(rules));
        }

        /// <summary>
        /// Builds a quote for a customer. Duplicate product lines are merged and every line is
        /// checked for existence, a positive quantity and sufficient stock.
        /// </summary>
        /// <exception cref="OrderValidationException">A product is unknown, a quantity is not positive, or stock is short.</exception>
        public async Task<Quote> GenerateQuoteAsync(Customer customer, IEnumerable<OrderLine> requested, CancellationToken ct = default)
        {
            var lines = MergeLines(requested);
            var ids = lines.Select(l => l.ProductId).ToList();

            // One query for the whole basket rather than one per line.
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            var priced = new List<(Product Product, int Quantity)>();
            foreach (var line in lines)
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                {
                    throw new OrderValidationException($"Product {line.ProductId} was not found.", isNotFound: true);
                }

                if (product.Stock < line.Quantity)
                {
                    throw new OrderValidationException(
                        $"Insufficient stock for {product.Name} (requested {line.Quantity}, available {product.Stock}).");
                }

                priced.Add((product, line.Quantity));
            }

            var rules = await GetActiveRulesAsync(customer.CustomerType, ct);
            return BuildQuote(customer, priced, rules);
        }

        /// <summary>
        /// Pure pricing calculation: no database access, so it is easy to unit test.
        /// </summary>
        public static Quote BuildQuote(Customer customer, IReadOnlyList<(Product Product, int Quantity)> lines, IReadOnlyList<PricingRule> rules)
        {
            var quote = new Quote
            {
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                CustomerType = customer.CustomerType,
                GeneratedAt = DateTime.UtcNow
            };

            // Discount 1: per-unit. Rounded to cents per unit so that unit price x quantity
            // always equals the stored line total.
            var unitRule = BestUnitRule(rules);
            foreach (var (product, quantity) in lines)
            {
                var unitPrice = ApplyUnitDiscount(product.BasePrice, unitRule);
                quote.Items.Add(new QuoteItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = quantity,
                    BasePrice = product.BasePrice,
                    DiscountedPrice = unitPrice,
                    DiscountAmount = (product.BasePrice - unitPrice) * quantity,
                    LineTotal = unitPrice * quantity
                });
            }

            quote.ListTotal = quote.Items.Sum(i => i.BasePrice * i.Quantity);
            quote.Subtotal = quote.Items.Sum(i => i.LineTotal);
            quote.UnitDiscountTotal = quote.ListTotal - quote.Subtotal;

            if (unitRule != null && quote.UnitDiscountTotal > 0)
            {
                quote.AppliedRules.Add(ToApplied(unitRule, RuleScope.PerUnit, quote.UnitDiscountTotal));
            }

            // Discount 2: volume, taken off the already-discounted subtotal.
            var volumeRules = rules.Where(r => r.MinimumOrderAmount.HasValue).ToList();
            var volumeRule = volumeRules
                .Where(r => quote.Subtotal >= r.MinimumOrderAmount!.Value)
                .OrderByDescending(r => r.DiscountPercentage)
                .FirstOrDefault();

            if (volumeRule != null)
            {
                quote.AdditionalDiscountPercentage = volumeRule.DiscountPercentage;
                quote.AdditionalDiscount = Round(quote.Subtotal * volumeRule.DiscountPercentage / 100m);
                quote.AppliedRules.Add(ToApplied(volumeRule, RuleScope.OrderTotal, quote.AdditionalDiscount));
            }

            quote.Total = quote.Subtotal - quote.AdditionalDiscount;
            quote.TotalSavings = quote.ListTotal - quote.Total;

            // Nudge: the cheapest tier above the current subtotal that would give a bigger discount.
            var currentPct = quote.AdditionalDiscountPercentage;
            var next = volumeRules
                .Where(r => quote.Subtotal < r.MinimumOrderAmount!.Value && r.DiscountPercentage > currentPct)
                .OrderBy(r => r.MinimumOrderAmount)
                .FirstOrDefault();
            if (next != null)
            {
                quote.NextTier = new NextTier
                {
                    Description = next.Description,
                    DiscountPercentage = next.DiscountPercentage,
                    MinimumOrderAmount = next.MinimumOrderAmount!.Value,
                    AmountToUnlock = next.MinimumOrderAmount.Value - quote.Subtotal
                };
            }

            return quote;
        }

        /// <summary>
        /// Merges duplicate product lines (summing quantities, in product-id order so stock
        /// updates always lock rows in the same order) and rejects non-positive quantities.
        /// </summary>
        public static List<OrderLine> MergeLines(IEnumerable<OrderLine> requested)
        {
            var merged = new SortedDictionary<int, long>();
            foreach (var line in requested)
            {
                if (line.Quantity <= 0)
                {
                    throw new OrderValidationException($"Quantity for product {line.ProductId} must be greater than zero.");
                }

                merged[line.ProductId] = merged.GetValueOrDefault(line.ProductId) + line.Quantity;
            }

            if (merged.Count == 0)
            {
                throw new OrderValidationException("At least one product is required.");
            }

            return merged
                .Select(kv => new OrderLine(kv.Key, (int)Math.Min(kv.Value, int.MaxValue)))
                .ToList();
        }

        private static PricingRule? BestUnitRule(IReadOnlyList<PricingRule> rules) =>
            rules.Where(r => !r.MinimumOrderAmount.HasValue)
                 .OrderByDescending(r => r.DiscountPercentage)
                 .FirstOrDefault();

        private static decimal ApplyUnitDiscount(decimal basePrice, PricingRule? rule) =>
            rule == null ? basePrice : Round(basePrice * rule.DiscountFactor);

        private static decimal Round(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static AppliedRule ToApplied(PricingRule rule, RuleScope scope, decimal saved) => new()
        {
            RuleId = rule.Id,
            Description = rule.Description,
            DiscountPercentage = rule.DiscountPercentage,
            Scope = scope,
            AmountSaved = saved
        };
    }
}
