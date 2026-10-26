namespace WholesaleRetailStore.Models
{
    /// <summary>
    /// A priced basket for one customer, with every discount that was applied itemised.
    /// </summary>
    /// <remarks>
    /// Two discounts can stack. First a per-unit rule (a rule with no minimum) reduces each
    /// unit price. Then a volume rule (a rule with a minimum order amount) is taken off the
    /// resulting subtotal once the subtotal reaches that minimum.
    /// </remarks>
    public class Quote
    {
        /// <summary>Customer ID this quote is for.</summary>
        public int CustomerId { get; set; }

        /// <summary>Customer name.</summary>
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>Customer type (drives which pricing rules apply).</summary>
        public CustomerType CustomerType { get; set; }

        /// <summary>When this quote was generated.</summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>Individual quote items with pricing details.</summary>
        public List<QuoteItem> Items { get; set; } = new();

        /// <summary>Sum of base price x quantity, before any discount.</summary>
        public decimal ListTotal { get; set; }

        /// <summary>Total saved by per-unit discounts across all lines.</summary>
        public decimal UnitDiscountTotal { get; set; }

        /// <summary>Sum of the line totals (list total minus per-unit discounts).</summary>
        public decimal Subtotal { get; set; }

        /// <summary>Order-level (volume) discount taken off the subtotal.</summary>
        public decimal AdditionalDiscount { get; set; }

        /// <summary>Percentage of the order-level discount, 0 when none applied.</summary>
        public decimal AdditionalDiscountPercentage { get; set; }

        /// <summary>Final amount payable.</summary>
        public decimal Total { get; set; }

        /// <summary>Everything saved versus list price (per-unit plus order-level).</summary>
        public decimal TotalSavings { get; set; }

        /// <summary>The rules that produced the discounts above.</summary>
        public List<AppliedRule> AppliedRules { get; set; } = new();

        /// <summary>The next volume tier the customer has not reached yet, if any.</summary>
        public NextTier? NextTier { get; set; }
    }

    /// <summary>
    /// An individual item in a quote with pricing details.
    /// </summary>
    public class QuoteItem
    {
        /// <summary>Product ID.</summary>
        public int ProductId { get; set; }

        /// <summary>Product name.</summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>Quantity requested.</summary>
        public int Quantity { get; set; }

        /// <summary>Base price per unit before discounts.</summary>
        public decimal BasePrice { get; set; }

        /// <summary>Price per unit after the per-unit discount.</summary>
        public decimal DiscountedPrice { get; set; }

        /// <summary>Total discount for this line (per-unit saving x quantity).</summary>
        public decimal DiscountAmount { get; set; }

        /// <summary>DiscountedPrice x Quantity.</summary>
        public decimal LineTotal { get; set; }
    }

    /// <summary>Whether a rule discounts each unit or the order total.</summary>
    public enum RuleScope
    {
        /// <summary>Reduces every unit price.</summary>
        PerUnit,

        /// <summary>Reduces the order subtotal.</summary>
        OrderTotal
    }

    /// <summary>A pricing rule that contributed to a quote, with the amount it saved.</summary>
    public class AppliedRule
    {
        /// <summary>Pricing rule ID.</summary>
        public int RuleId { get; set; }

        /// <summary>Rule description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Discount percentage of the rule.</summary>
        public decimal DiscountPercentage { get; set; }

        /// <summary>Whether it discounted each unit or the order total.</summary>
        public RuleScope Scope { get; set; }

        /// <summary>Amount this rule saved on the quote.</summary>
        public decimal AmountSaved { get; set; }
    }

    /// <summary>The next volume discount tier and how far the order is from it.</summary>
    public class NextTier
    {
        /// <summary>Rule description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Discount percentage that would be unlocked.</summary>
        public decimal DiscountPercentage { get; set; }

        /// <summary>Subtotal needed to unlock it.</summary>
        public decimal MinimumOrderAmount { get; set; }

        /// <summary>How much more the customer needs to add to the subtotal.</summary>
        public decimal AmountToUnlock { get; set; }
    }
}
