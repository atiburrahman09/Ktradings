using System;
using System.Threading.Tasks;
using KTrading.Data;
using KTrading.Models;
using KTrading.Pages.SalesOrders;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class SalesOrderReturnAdjustmentTests
{
    [Fact]
    public async Task Details_DeductsReturnQuantityAndDamageQuantityFromSalesOrderTotal()
    {
        await using var db = CreateDbContext();
        var product = new Product { Id = Guid.NewGuid(), Name = "Ball", Price = 10m, Cost = 6m };
        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = "SO-DAMAGE",
            CustomerId = Guid.NewGuid(),
            OrderDate = DateTimeOffset.UtcNow,
            Total = 60m
        };
        var productReturn = new ProductReturn
        {
            Id = Guid.NewGuid(),
            ReturnNumber = "R-DAMAGE",
            SalesOrderId = order.Id,
            CustomerId = order.CustomerId,
            Status = "Processed",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Products.Add(product);
        db.SalesOrders.Add(order);
        db.SalesOrderItems.Add(new SalesOrderItem
        {
            Id = Guid.NewGuid(),
            SalesOrderId = order.Id,
            ProductId = product.Id,
            Quantity = 6m,
            UnitPrice = 10m,
            LineTotal = 60m
        });
        db.ProductReturns.Add(productReturn);
        db.ProductReturnItems.Add(new ProductReturnItem
        {
            Id = Guid.NewGuid(),
            ProductReturnId = productReturn.Id,
            ProductId = product.Id,
            Quantity = 3m,
            DamagedQuantity = 2m
        });
        await db.SaveChangesAsync();

        var page = new DetailsModel(db);

        await page.OnGetAsync(order.Id);

        Assert.Equal(50m, page.ReturnedAmount);
        Assert.Equal(10m, page.AdjustedTotal);
        var displayItem = Assert.Single(page.DisplayItems);
        Assert.Equal(3m, displayItem.Quantity);
        Assert.Equal(10m, displayItem.LineTotal);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
