using System.ComponentModel.DataAnnotations;

namespace WholesaleRetailStore.Models.DTOs
{
    /// <summary>
    /// Data Transfer Object for creating a new order
    /// </summary>
    public class CreateOrderDto
    {
        /// <summary>
        /// Customer ID placing the order
        /// </summary>
        [Required]
        public int CustomerId { get; set; }

        /// <summary>
        /// List of items to include in the order. The same product may appear more than once;
        /// the quantities are added together.
        /// </summary>
        [Required]
        [MinLength(1)]
        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
    }

    /// <summary>
    /// Data Transfer Object for order items
    /// </summary>
    public class OrderItemDto
    {
        /// <summary>
        /// Product ID to order
        /// </summary>
        [Required]
        public int ProductId { get; set; }

        /// <summary>
        /// Quantity of the product to order
        /// </summary>
        [Required]
        [Range(1, 1_000_000)]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Data Transfer Object for order responses
    /// </summary>
    public class OrderDto
    {
        /// <summary>Unique identifier for the order.</summary>
        public int Id { get; set; }

        /// <summary>Customer ID who placed this order.</summary>
        public int CustomerId { get; set; }

        /// <summary>Customer name.</summary>
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>Customer type (affects pricing).</summary>
        public CustomerType CustomerType { get; set; }

        /// <summary>Date and time when the order was created.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Sum of the line totals, before the order-level discount.</summary>
        public decimal Subtotal { get; set; }

        /// <summary>Order-level (volume) discount.</summary>
        public decimal DiscountAmount { get; set; }

        /// <summary>The rule behind <see cref="DiscountAmount"/>, if any.</summary>
        public string? DiscountDescription { get; set; }

        /// <summary>Amount paid: <see cref="Subtotal"/> minus <see cref="DiscountAmount"/>.</summary>
        public decimal TotalAmount { get; set; }

        /// <summary>Current status of the order.</summary>
        public OrderStatus Status { get; set; }

        /// <summary>Items included in this order.</summary>
        public List<OrderItemDetailDto> Items { get; set; } = new List<OrderItemDetailDto>();

        /// <summary>
        /// Maps an order (loaded with its customer, items and products) to its response shape.
        /// </summary>
        public static OrderDto FromOrder(Order order) => new()
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CustomerName = order.Customer?.Name ?? string.Empty,
            CustomerType = order.Customer?.CustomerType ?? CustomerType.Retail,
            CreatedAt = order.CreatedAt,
            Subtotal = order.OrderItems.Sum(i => i.Quantity * i.UnitPrice),
            DiscountAmount = order.DiscountAmount,
            DiscountDescription = order.DiscountDescription,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            Items = order.OrderItems.Select(oi => new OrderItemDetailDto
            {
                ProductId = oi.ProductId,
                ProductName = oi.Product?.Name ?? string.Empty,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                TotalPrice = oi.Quantity * oi.UnitPrice
            }).ToList()
        };
    }

    /// <summary>
    /// Data Transfer Object for order item details in responses
    /// </summary>
    public class OrderItemDetailDto
    {
        /// <summary>Product ID.</summary>
        public int ProductId { get; set; }

        /// <summary>Product name.</summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>Quantity ordered.</summary>
        public int Quantity { get; set; }

        /// <summary>Unit price at time of order.</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>Total price for this item (Quantity * UnitPrice).</summary>
        public decimal TotalPrice { get; set; }
    }

    /// <summary>
    /// DTO for updating order status
    /// </summary>
    public class UpdateOrderStatusDto
    {
        /// <summary>
        /// New status for the order, by name (Pending, Confirmed, Shipped, Cancelled).
        /// </summary>
        [Required]
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for quote generation requests
    /// </summary>
    public class QuoteRequestDto
    {
        /// <summary>Customer ID for the quote.</summary>
        [Required]
        public int CustomerId { get; set; }

        /// <summary>Items to price.</summary>
        [Required]
        [MinLength(1)]
        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
    }

    /// <summary>
    /// DTO for pricing comparison requests
    /// </summary>
    public class ComparePricingDto
    {
        /// <summary>Product IDs to compare pricing for.</summary>
        [Required]
        [MinLength(1)]
        public List<int> ProductIds { get; set; } = new List<int>();
    }
}
