using System;
using System.Linq;
using System.Threading.Tasks;
using KTrading.Data;
using KTrading.Models;
using KTrading.Pages.StockDamages;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class StockDamageIndexTests
{
    [Fact]
    public async Task OnGetAsync_IncludesDamagedSalesOrderReturnItems()
    {
        await using var db = CreateDbContext();
        var product = new Product { Id = Guid.NewGuid(), Name = "Damaged Product", Price = 10m, Cost = 7m };
        var productReturn = new ProductReturn
        {
            Id = Guid.NewGuid(),
            ReturnNumber = "R-DAM-1",
            Status = "Open",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var returnItem = new ProductReturnItem
        {
            Id = Guid.NewGuid(),
            ProductReturnId = productReturn.Id,
            ProductId = product.Id,
            Quantity = 3m,
            DamagedQuantity = 2m,
            Notes = "Broken from sales order"
        };
        db.Products.Add(product);
        db.ProductReturns.Add(productReturn);
        db.ProductReturnItems.Add(returnItem);
        await db.SaveChangesAsync();

        var page = new IndexModel(db);

        await page.OnGetAsync(null);

        var damage = Assert.Single(page.Damages);
        Assert.Equal(product.Id, damage.ProductId);
        Assert.Equal(2m, damage.Quantity);
        Assert.Contains("Broken from sales order", damage.Note);
    }

    [Fact]
    public async Task OnGetAsync_DoesNotDuplicateReturnDamageAlreadyRecordedAsMovement()
    {
        await using var db = CreateDbContext();
        var product = new Product { Id = Guid.NewGuid(), Name = "Processed Damage", Price = 10m, Cost = 7m };
        var productReturn = new ProductReturn
        {
            Id = Guid.NewGuid(),
            ReturnNumber = "R-DAM-2",
            Status = "Processed",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        db.Products.Add(product);
        db.ProductReturns.Add(productReturn);
        db.ProductReturnItems.Add(new ProductReturnItem
        {
            Id = Guid.NewGuid(),
            ProductReturnId = productReturn.Id,
            ProductId = product.Id,
            Quantity = 1m,
            DamagedQuantity = 1m
        });
        db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = -1m,
            MovementType = "DAMAGE",
            ReferenceId = productReturn.Id,
            Note = "Processed damaged return",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var page = new IndexModel(db);

        await page.OnGetAsync(null);

        Assert.Single(page.Damages);
        Assert.Equal("Processed damaged return", page.Damages.Single().Note);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
