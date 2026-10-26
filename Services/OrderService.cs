using Microsoft.EntityFrameworkCore;
using WholesaleRetailStore.Data;
using WholesaleRetailStore.Models;

namespace WholesaleRetailStore.Services
{
    /// <summary>
    /// Places orders. Pricing, stock decrement and the order insert happen in one transaction,
    /// so an order never exists without its stock being taken (or the other way round).
    /// </summary>
    public class OrderService
    {
        private readonly AppDbContext _context;
        private readonly PricingService _pricing;

        public OrderService(AppDbContext context, PricingService pricing)
        {
            _context = context;
            _pricing = pricing;
        }

        /// <summary>
        /// Prices the basket, takes the stock and saves the order atomically.
        /// </summary>
        /// <exception cref="OrderValidationException">Customer or product unknown, bad quantity, or insufficient stock.</exception>
        public async Task<Order> PlaceOrderAsync(int customerId, IEnumerable<OrderLine> items, CancellationToken ct = default)
        {
            var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct)
                ?? throw new OrderValidationException("Customer not found.", isNotFound: true);

            var requested = items.ToList();

            // A user-started transaction has to run inside the execution strategy because
            // Program.cs enables retry-on-failure; the whole unit is replayed if a transient error occurs.
            var strategy = _context.Database.CreateExecutionStrategy();
            var orderId = await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                var quote = await _pricing.GenerateQuoteAsync(customer, requested, ct);

                // Guarded decrement: the WHERE clause makes the stock check and the update one
                // atomic statement, so two concurrent orders can't both take the last unit.
                // Lines are in product-id order, so concurrent orders lock rows in the same order.
                foreach (var item in quote.Items)
                {
                    var productId = item.ProductId;
                    var quantity = item.Quantity;
                    var updated = await _context.Products
                        .Where(p => p.Id == productId && p.Stock >= quantity)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - quantity), ct);

                    if (updated == 0)
                    {
                        throw new OrderValidationException($"Insufficient stock for {item.ProductName}.");
                    }
                }

                var order = new Order
                {
                    CustomerId = customer.Id,
                    CreatedAt = DateTime.UtcNow,
                    Status = OrderStatus.Confirmed,
                    TotalAmount = quote.Total,
                    DiscountAmount = quote.AdditionalDiscount,
                    DiscountDescription = quote.AppliedRules
                        .FirstOrDefault(r => r.Scope == RuleScope.OrderTotal)?.Description,
                    OrderItems = quote.Items.Select(i => new OrderItem
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.DiscountedPrice
                    }).ToList()
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return order.Id;
            });

            return (await GetOrderAsync(orderId, ct))!;
        }

        /// <summary>
        /// Changes an order's status. Moving to <see cref="OrderStatus.Cancelled"/> returns the
        /// order's stock; moving back out of Cancelled takes it again (and fails if it is gone).
        /// The status and the stock change commit together.
        /// </summary>
        /// <returns>The updated order, or null if it does not exist.</returns>
        /// <exception cref="OrderValidationException">Re-opening a cancelled order but stock is no longer available.</exception>
        public async Task<Order?> UpdateStatusAsync(int id, OrderStatus newStatus, CancellationToken ct = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            var found = await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                var order = await _context.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == id, ct);
                if (order == null) return false;

                var cancelling = newStatus == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled;
                var reopening = newStatus != OrderStatus.Cancelled && order.Status == OrderStatus.Cancelled;

                // Same product-id order as placing an order, so concurrent updates lock rows consistently.
                foreach (var item in order.OrderItems.OrderBy(i => i.ProductId))
                {
                    var productId = item.ProductId;
                    var quantity = item.Quantity;

                    if (cancelling)
                    {
                        await _context.Products
                            .Where(p => p.Id == productId)
                            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + quantity), ct);
                    }
                    else if (reopening)
                    {
                        var updated = await _context.Products
                            .Where(p => p.Id == productId && p.Stock >= quantity)
                            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - quantity), ct);
                        if (updated == 0)
                        {
                            throw new OrderValidationException("Cannot reopen this order: there is not enough stock left.");
                        }
                    }
                }

                order.Status = newStatus;
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return true;
            });

            return found ? await GetOrderAsync(id, ct) : null;
        }

        /// <summary>
        /// Loads an order with its customer and items (read-only).
        /// </summary>
        public Task<Order?> GetOrderAsync(int id, CancellationToken ct = default) =>
            OrdersWithDetails().FirstOrDefaultAsync(o => o.Id == id, ct);

        /// <summary>
        /// Loads all orders with their customer and items, newest first (read-only).
        /// </summary>
        public Task<List<Order>> GetOrdersAsync(CancellationToken ct = default) =>
            OrdersWithDetails().OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id).ToListAsync(ct);

        private IQueryable<Order> OrdersWithDetails() =>
            _context.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product);
    }
}
