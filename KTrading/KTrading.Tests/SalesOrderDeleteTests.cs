using System;
using System.Linq;
using System.Threading.Tasks;
using KTrading.Data;
using KTrading.Models;
using KTrading.Pages.SalesOrders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class SalesOrderDeleteTests
{
    [Fact]
    public async Task OnPostDeleteAsync_RollsBackStock_AndDeletesRelatedRecords()
    {
        await using var db = CreateDbContext();
        var product = new Product { Id = Guid.NewGuid(), Name = "Rollback Product", Price = 10m, Cost = 7m };
        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = "SO-DELETE",
            CustomerId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            OrderDate = DateTimeOffset.UtcNow,
            Total = 40m,
            PaidAmount = 10m,
            DueAmount = 30m
        };
        var productReturn = new ProductReturn
        {
            Id = Guid.NewGuid(),
            ReturnNumber = "R-SO-DELETE",
            SalesOrderId = order.Id,
            CustomerId = order.CustomerId,
            Status = "Processed",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Products.Add(product);
        db.Stocks.Add(new Stock { Id = Guid.NewGuid(), ProductId = product.Id, Quantity = 7m, UpdatedAt = DateTimeOffset.UtcNow });
        db.SalesOrders.Add(order);
        db.SalesOrderItems.Add(new SalesOrderItem
        {
            Id = Guid.NewGuid(),
            SalesOrderId = order.Id,
            ProductId = product.Id,
            Quantity = 4m,
            UnitPrice = 10m,
            LineTotal = 40m
        });
        db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = -4m,
            MovementType = "OUT",
            ReferenceId = order.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.ProductReturns.Add(productReturn);
        db.ProductReturnItems.Add(new ProductReturnItem
        {
            Id = Guid.NewGuid(),
            ProductReturnId = productReturn.Id,
            ProductId = product.Id,
            Quantity = 1m
        });
        db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = 1m,
            MovementType = "RETURN",
            ReferenceId = productReturn.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            SalesOrderId = order.Id,
            Amount = 10m,
            PaymentDate = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var page = new IndexModel(db);

        var result = await page.OnPostDeleteAsync(order.Id, null, null, null, null, null);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(10m, (await db.Stocks.SingleAsync(s => s.ProductId == product.Id)).Quantity);
        Assert.False(await db.SalesOrders.AnyAsync(o => o.Id == order.Id));
        Assert.False(await db.SalesOrderItems.AnyAsync(i => i.SalesOrderId == order.Id));
        Assert.False(await db.Payments.AnyAsync(p => p.SalesOrderId == order.Id));
        Assert.False(await db.ProductReturns.AnyAsync(r => r.Id == productReturn.Id));
        Assert.False(await db.ProductReturnItems.AnyAsync(i => i.ProductReturnId == productReturn.Id));
        Assert.False(await db.StockMovements.AnyAsync(m => m.ReferenceId == order.Id || m.ReferenceId == productReturn.Id));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
