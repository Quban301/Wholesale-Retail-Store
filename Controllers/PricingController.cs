using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WholesaleRetailStore.Data;
using WholesaleRetailStore.Models;
using WholesaleRetailStore.Models.DTOs;
using WholesaleRetailStore.Services;

namespace WholesaleRetailStore.Controllers
{
    /// <summary>
    /// REST API for pricing: quotes, the active rules, and per-customer product prices.
    /// </summary>
    [ApiController]
    public class PricingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PricingService _pricing;

        public PricingController(AppDbContext context, PricingService pricing)
        {
            _context = context;
            _pricing = pricing;
        }

        /// <summary>
        /// GET: api/pricing/quote/{customerId}?productId=1&amp;quantity=5&amp;productId=2&amp;quantity=3
        /// Generates a quote from parallel productId / quantity lists.
        /// </summary>
        [HttpGet("api/pricing/quote/{customerId:int}")]
        public async Task<IActionResult> GetQuote(int customerId, [FromQuery] List<int> productId, [FromQuery] List<int> quantity)
        {
            if (productId.Count == 0 || productId.Count != quantity.Count)
            {
                return BadRequest(new { message = "Product IDs and quantities must be provided and have the same count" });
            }

            var lines = productId.Zip(quantity, (p, q) => new OrderLine(p, q));
            return await QuoteFor(customerId, lines);
        }

        /// <summary>
        /// POST: api/pricing/quote
        /// Generates a quote using a structured request body. The response lists every discount
        /// that was applied and, if there is one, the next volume tier.
        /// </summary>
        [HttpPost("api/pricing/quote")]
        public Task<IActionResult> GenerateQuote([FromBody] QuoteRequestDto request) =>
            QuoteFor(request.CustomerId, request.Items.Select(i => new OrderLine(i.ProductId, i.Quantity)));

        /// <summary>
        /// GET: api/pricing/rules
        /// Returns all active pricing rules
        /// </summary>
        [HttpGet("api/pricing/rules")]
        public async Task<IActionResult> GetPricingRules()
        {
            var rules = await _context.PricingRules
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.CustomerType)
                .ThenBy(r => r.MinimumOrderAmount ?? 0)
                .Select(r => new
                {
                    r.Id,
                    r.CustomerType,
                    r.DiscountPercentage,
                    r.MinimumOrderAmount,
                    r.Description
                })
                .ToListAsync();

            return Ok(rules);
        }

        /// <summary>
        /// GET: api/pricing/products/{customerId}
        /// Returns every product with the unit price this customer pays (after the per-unit rule).
        /// </summary>
        [HttpGet("api/pricing/products/{customerId:int}")]
        public async Task<IActionResult> GetProductPrices(int customerId)
        {
            var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            var rules = await _pricing.GetActiveRulesAsync(customer.CustomerType);
            var products = await _context.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();

            return Ok(new
            {
                CustomerId = customerId,
                CustomerName = customer.Name,
                customer.CustomerType,
                Products = products.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Stock,
                    p.BasePrice,
                    CalculatedPrice = PricingService.CalculateUnitPrice(p, rules)
                })
            });
        }

        /// <summary>
        /// POST: api/pricing/compare
        /// Compares retail and wholesale unit prices for the same products
        /// </summary>
        [HttpPost("api/pricing/compare")]
        public async Task<IActionResult> ComparePricing([FromBody] ComparePricingDto request)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => request.ProductIds.Contains(p.Id))
                .ToListAsync();

            var retailRules = await _pricing.GetActiveRulesAsync(CustomerType.Retail);
            var wholesaleRules = await _pricing.GetActiveRulesAsync(CustomerType.Wholesale);

            var comparison = products.Select(p =>
            {
                var retail = PricingService.CalculateUnitPrice(p, retailRules);
                var wholesale = PricingService.CalculateUnitPrice(p, wholesaleRules);
                return new
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    p.BasePrice,
                    RetailPrice = retail,
                    WholesalePrice = wholesale,
                    WholesaleDiscount = p.BasePrice - wholesale,
                    WholesaleDiscountPercentage = p.BasePrice == 0 ? 0 : (p.BasePrice - wholesale) / p.BasePrice * 100
                };
            });

            return Ok(new { request.ProductIds, Comparison = comparison });
        }

        private async Task<IActionResult> QuoteFor(int customerId, IEnumerable<OrderLine> lines)
        {
            var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            try
            {
                return Ok(await _pricing.GenerateQuoteAsync(customer, lines));
            }
            catch (OrderValidationException ex)
            {
                return ex.IsNotFound ? NotFound(new { message = ex.Message }) : BadRequest(new { message = ex.Message });
            }
        }
    }
}
