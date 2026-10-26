using WholesaleRetailStore.Models;
using WholesaleRetailStore.Services;

namespace WholesaleRetailStore.Tests;

public class PricingTests
{
    private static readonly Customer Retail = new() { Id = 1, Name = "Rita", CustomerType = CustomerType.Retail };
    private static readonly Customer Wholesale = new() { Id = 3, Name = "Walter Ltd", CustomerType = CustomerType.Wholesale };

    // The seeded wholesale rules: 10% per unit, plus 15% on orders from R500.
    private static readonly PricingRule Standard = new()
    { Id = 1, CustomerType = CustomerType.Wholesale, DiscountPercentage = 10, Description = "Standard wholesale", IsActive = true };
    private static readonly PricingRule Volume = new()
    { Id = 2, CustomerType = CustomerType.Wholesale, DiscountPercentage = 15, MinimumOrderAmount = 500, Description = "Volume", IsActive = true };

    private static Product Item(int id, decimal price) => new() { Id = id, Name = $"P{id}", BasePrice = price, Stock = 1000 };

    [Fact]
    public void Retail_customer_pays_list_price_when_no_retail_rules_exist()
    {
        var quote = PricingService.BuildQuote(Retail, [(Item(1, 99.99m), 2)], []);

        Assert.Equal(99.99m, quote.Items[0].DiscountedPrice);
        Assert.Equal(199.98m, quote.Total);
        Assert.Equal(0, quote.TotalSavings);
        Assert.Empty(quote.AppliedRules);
    }

    [Fact]
    public void Wholesale_small_order_gets_only_the_per_unit_discount()
    {
        var quote = PricingService.BuildQuote(Wholesale, [(Item(1, 100m), 2)], [Standard, Volume]);

        Assert.Equal(90m, quote.Items[0].DiscountedPrice);
        Assert.Equal(180m, quote.Subtotal);
        Assert.Equal(0, quote.AdditionalDiscount);
        Assert.Equal(180m, quote.Total);
        Assert.Single(quote.AppliedRules);
        Assert.Equal(RuleScope.PerUnit, quote.AppliedRules[0].Scope);
    }

    [Fact]
    public void Wholesale_large_order_stacks_both_discounts()
    {
        // 6 x 199.99: unit price 179.99 (10% off), subtotal 1079.94, then 15% off that = 161.99.
        var quote = PricingService.BuildQuote(Wholesale, [(Item(2, 199.99m), 6)], [Standard, Volume]);

        Assert.Equal(179.99m, quote.Items[0].DiscountedPrice);
        Assert.Equal(1079.94m, quote.Subtotal);
        Assert.Equal(161.99m, quote.AdditionalDiscount);
        Assert.Equal(917.95m, quote.Total);
        Assert.Equal(1199.94m - 917.95m, quote.TotalSavings);
        Assert.Equal([RuleScope.PerUnit, RuleScope.OrderTotal], quote.AppliedRules.Select(r => r.Scope));
        Assert.Null(quote.NextTier);
    }

    [Fact]
    public void Volume_discount_applies_exactly_at_the_minimum()
    {
        // No per-unit rule, subtotal is exactly the 500 minimum.
        var quote = PricingService.BuildQuote(Wholesale, [(Item(1, 250m), 2)], [Volume]);

        Assert.Equal(500m, quote.Subtotal);
        Assert.Equal(75m, quote.AdditionalDiscount);
    }

    [Fact]
    public void Volume_discount_does_not_apply_just_below_the_minimum()
    {
        var quote = PricingService.BuildQuote(Wholesale, [(Item(1, 249.99m), 2)], [Volume]);

        Assert.Equal(0, quote.AdditionalDiscount);
        Assert.NotNull(quote.NextTier);
        Assert.Equal(0.02m, quote.NextTier!.AmountToUnlock);
    }

    [Fact]
    public void Best_rule_wins_when_several_rules_of_a_kind_exist()
    {
        var better = new PricingRule { Id = 9, CustomerType = CustomerType.Wholesale, DiscountPercentage = 20, Description = "Gold", IsActive = true };

        var quote = PricingService.BuildQuote(Wholesale, [(Item(1, 100m), 1)], [Standard, better]);

        Assert.Equal(80m, quote.Items[0].DiscountedPrice);
    }

    [Fact]
    public void Unit_prices_are_rounded_to_cents_so_lines_add_up()
    {
        // 12.99 * 0.9 = 11.691 -> 11.69
        var quote = PricingService.BuildQuote(Wholesale, [(Item(4, 12.99m), 3)], [Standard]);

        Assert.Equal(11.69m, quote.Items[0].DiscountedPrice);
        Assert.Equal(35.07m, quote.Items[0].LineTotal);
        Assert.Equal(quote.Items.Sum(i => i.LineTotal), quote.Subtotal);
    }

    [Fact]
    public void Duplicate_lines_are_merged_in_product_order()
    {
        var merged = PricingService.MergeLines([new(5, 1), new(2, 3), new(5, 4)]);

        Assert.Equal([new OrderLine(2, 3), new OrderLine(5, 5)], merged);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Non_positive_quantities_are_rejected(int quantity)
    {
        Assert.Throws<OrderValidationException>(() => PricingService.MergeLines([new(1, quantity)]));
    }

    [Fact]
    public void An_empty_basket_is_rejected()
    {
        Assert.Throws<OrderValidationException>(() => PricingService.MergeLines([]));
    }
}
