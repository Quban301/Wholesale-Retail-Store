using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WholesaleRetailStore.Data;
using WholesaleRetailStore.Models;
using WholesaleRetailStore.Services;

namespace WholesaleRetailStore.Tests;

/// <summary>
/// Runs the real order flow against an in-memory SQLite database seeded from the app's own seed data
/// (customers 1-2 retail, 3-4 wholesale; product 2 is a 199.99 Smart Watch with 30 in stock).
/// </summary>
public sealed class OrderServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public OrderServiceTests()
    {
        _connection.Open();
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private OrderService NewService(AppDbContext context) => new(context, new PricingService(context));

    private int StockOf(int productId)
    {
        using var context = NewContext();
        return context.Products.Single(p => p.Id == productId).Stock;
    }

    [Fact]
    public async Task Placing_an_order_takes_stock_and_the_totals_reconcile()
    {
        await using var context = NewContext();

        var order = await NewService(context).PlaceOrderAsync(3, [new(2, 6)]);

        Assert.Equal(24, StockOf(2));
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(917.95m, order.TotalAmount);
        Assert.Equal(161.99m, order.DiscountAmount);
        Assert.Equal("Volume discount - 15% off for orders over R500", order.DiscountDescription);
        Assert.Equal(order.TotalAmount, order.OrderItems.Sum(i => i.Quantity * i.UnitPrice) - order.DiscountAmount);
    }

    [Fact]
    public async Task Retail_customer_pays_list_price()
    {
        await using var context = NewContext();

        var order = await NewService(context).PlaceOrderAsync(1, [new(2, 1)]);

        Assert.Equal(199.99m, order.TotalAmount);
        Assert.Equal(0, order.DiscountAmount);
    }

    [Fact]
    public async Task Insufficient_stock_rejects_the_order_and_changes_nothing()
    {
        await using var context = NewContext();

        // Product 1 has plenty; product 2 only has 30, so the second line fails.
        var ex = await Assert.ThrowsAsync<OrderValidationException>(
            () => NewService(context).PlaceOrderAsync(3, [new(1, 5), new(2, 31)]));

        Assert.Contains("Insufficient stock", ex.Message);
        Assert.Equal(50, StockOf(1));
        Assert.Equal(30, StockOf(2));
        await using var check = NewContext();
        Assert.Empty(check.Orders);
    }

    [Fact]
    public async Task The_last_unit_can_only_be_sold_once()
    {
        await using (var setup = NewContext())
        {
            await setup.Products.Where(p => p.Id == 2).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 1));
        }

        await using var first = NewContext();
        await using var second = NewContext();
        await NewService(first).PlaceOrderAsync(1, [new(2, 1)]);

        await Assert.ThrowsAsync<OrderValidationException>(() => NewService(second).PlaceOrderAsync(2, [new(2, 1)]));

        Assert.Equal(0, StockOf(2));
    }

    [Fact]
    public async Task Duplicate_product_lines_are_added_together()
    {
        await using var context = NewContext();

        var order = await NewService(context).PlaceOrderAsync(1, [new(4, 2), new(4, 3)]);

        Assert.Single(order.OrderItems);
        Assert.Equal(5, order.OrderItems.Single().Quantity);
        Assert.Equal(195, StockOf(4));
    }

    [Fact]
    public async Task Unknown_customer_or_product_is_reported_as_not_found()
    {
        await using var context = NewContext();
        var service = NewService(context);

        var noCustomer = await Assert.ThrowsAsync<OrderValidationException>(() => service.PlaceOrderAsync(999, [new(1, 1)]));
        var noProduct = await Assert.ThrowsAsync<OrderValidationException>(() => service.PlaceOrderAsync(1, [new(999, 1)]));

        Assert.True(noCustomer.IsNotFound);
        Assert.True(noProduct.IsNotFound);
    }

    [Fact]
    public async Task Inactive_rules_are_ignored()
    {
        await using (var setup = NewContext())
        {
            await setup.PricingRules.ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false));
        }

        await using var context = NewContext();
        var order = await NewService(context).PlaceOrderAsync(3, [new(2, 6)]);

        Assert.Equal(6 * 199.99m, order.TotalAmount);
    }

    [Fact]
    public async Task Cancelling_an_order_returns_its_stock()
    {
        await using var context = NewContext();
        var service = NewService(context);
        var order = await service.PlaceOrderAsync(3, [new(2, 6), new(4, 10)]);
        Assert.Equal(24, StockOf(2));
        Assert.Equal(190, StockOf(4));

        var cancelled = await service.UpdateStatusAsync(order.Id, OrderStatus.Cancelled);

        Assert.Equal(OrderStatus.Cancelled, cancelled!.Status);
        Assert.Equal(30, StockOf(2));
        Assert.Equal(200, StockOf(4));
    }

    [Fact]
    public async Task Cancelling_twice_only_returns_stock_once()
    {
        await using var context = NewContext();
        var service = NewService(context);
        var order = await service.PlaceOrderAsync(1, [new(2, 5)]);

        await service.UpdateStatusAsync(order.Id, OrderStatus.Cancelled);
        await service.UpdateStatusAsync(order.Id, OrderStatus.Cancelled);

        Assert.Equal(30, StockOf(2));
    }

    [Fact]
    public async Task Other_status_changes_leave_stock_alone()
    {
        await using var context = NewContext();
        var service = NewService(context);
        var order = await service.PlaceOrderAsync(1, [new(2, 5)]);

        await service.UpdateStatusAsync(order.Id, OrderStatus.Shipped);

        Assert.Equal(25, StockOf(2));
    }

    [Fact]
    public async Task Reopening_a_cancelled_order_takes_the_stock_again()
    {
        await using var context = NewContext();
        var service = NewService(context);
        var order = await service.PlaceOrderAsync(1, [new(2, 5)]);
        await service.UpdateStatusAsync(order.Id, OrderStatus.Cancelled);

        await service.UpdateStatusAsync(order.Id, OrderStatus.Confirmed);

        Assert.Equal(25, StockOf(2));
    }

    [Fact]
    public async Task Reopening_fails_and_changes_nothing_if_the_stock_was_sold_meanwhile()
    {
        await using var context = NewContext();
        var service = NewService(context);
        var order = await service.PlaceOrderAsync(1, [new(2, 5)]);
        await service.UpdateStatusAsync(order.Id, OrderStatus.Cancelled);
        await service.PlaceOrderAsync(2, [new(2, 30)]);   // someone else buys everything

        await Assert.ThrowsAsync<OrderValidationException>(() => service.UpdateStatusAsync(order.Id, OrderStatus.Confirmed));

        Assert.Equal(0, StockOf(2));
        await using var check = NewContext();
        Assert.Equal(OrderStatus.Cancelled, check.Orders.Single(o => o.Id == order.Id).Status);
    }

    [Fact]
    public async Task Updating_a_missing_order_returns_null()
    {
        await using var context = NewContext();

        Assert.Null(await NewService(context).UpdateStatusAsync(999, OrderStatus.Shipped));
    }
}
