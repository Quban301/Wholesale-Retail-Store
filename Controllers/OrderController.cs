using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WholesaleRetailStore.Data;
using WholesaleRetailStore.Models;
using WholesaleRetailStore.Models.DTOs;
using WholesaleRetailStore.Services;

namespace WholesaleRetailStore.Controllers
{
    /// <summary>
    /// REST API for orders: placing them (with validation and stock decrement) and reading them back.
    /// </summary>
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly OrderService _orders;
        private readonly ILogger<OrderController> _logger;

        public OrderController(AppDbContext context, OrderService orders, ILogger<OrderController> logger)
        {
            _context = context;
            _orders = orders;
            _logger = logger;
        }

        /// <summary>
        /// GET: api/orders
        /// Returns all orders, newest first
        /// </summary>
        [HttpGet("api/orders")]
        public async Task<IActionResult> GetOrders()
        {
            var orders = await _orders.GetOrdersAsync();
            return Ok(orders.Select(OrderDto.FromOrder));
        }

        /// <summary>
        /// GET: api/orders/{id}
        /// Returns a specific order by ID
        /// </summary>
        [HttpGet("api/orders/{id:int}")]
        public async Task<IActionResult> GetOrder(int id)
        {
            var order = await _orders.GetOrderAsync(id);
            if (order == null)
            {
                return NotFound(new { message = "Order not found" });
            }

            return Ok(OrderDto.FromOrder(order));
        }

        /// <summary>
        /// POST: api/orders
        /// Places an order. Prices it for the customer's type, checks and takes stock, and saves
        /// everything in one transaction. Returns 400 if a quantity is invalid or stock is short.
        /// </summary>
        [HttpPost("api/orders")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto orderDto)
        {
            try
            {
                var lines = orderDto.Items.Select(i => new OrderLine(i.ProductId, i.Quantity));
                var order = await _orders.PlaceOrderAsync(orderDto.CustomerId, lines);

                _logger.LogInformation("Order {OrderId} placed for customer {CustomerId}", order.Id, order.CustomerId);
                return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, OrderDto.FromOrder(order));
            }
            catch (OrderValidationException ex)
            {
                return ex.IsNotFound ? NotFound(new { message = ex.Message }) : BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// PUT: api/orders/{id}/status
        /// Updates the status of an order
        /// </summary>
        [HttpPut("api/orders/{id:int}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto statusDto)
        {
            // Enum.TryParse also accepts numbers ("99"), so require a defined, non-numeric name.
            if (int.TryParse(statusDto.Status, out _) ||
                !Enum.TryParse<OrderStatus>(statusDto.Status, ignoreCase: true, out var newStatus) ||
                !Enum.IsDefined(newStatus))
            {
                return BadRequest(new { message = "Invalid order status" });
            }

            try
            {
                // Cancelling returns the order's stock, so this goes through the service.
                var order = await _orders.UpdateStatusAsync(id, newStatus);
                return order == null
                    ? NotFound(new { message = "Order not found" })
                    : Ok(OrderDto.FromOrder(order));
            }
            catch (OrderValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
