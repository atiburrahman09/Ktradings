using System;
using System.Linq;
using System.Threading.Tasks;
using KTrading.Data;
using KTrading.Models;
using KTrading.Pages.StockDamages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
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

    [Fact]
    public async Task OnPostDeleteAsync_RemovesManualDamageMovement_AndLeavesStockUnchanged()
    {
        await using var db = CreateDbContext();
        var product = new Product { Id = Guid.NewGuid(), Name = "Delete Damage", Price = 10m, Cost = 7m };
        var stock = new Stock { Id = Guid.NewGuid(), ProductId = product.Id, Quantity = 5m, UpdatedAt = DateTimeOffset.UtcNow };
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = -2m,
            MovementType = "DAMAGE",
            ReferenceId = stock.Id,
            Note = "Manual damage",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Products.Add(product);
        db.Stocks.Add(stock);
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync();

        var page = new IndexModel(db);

        var result = await page.OnPostDeleteAsync(movement.Id, null, null);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.False(await db.StockMovements.AnyAsync(m => m.Id == movement.Id));
        Assert.Equal(5m, (await db.Stocks.SingleAsync(s => s.ProductId == product.Id)).Quantity);
    }

    [Fact]
    public async Task Create_OnPostAsync_RecordsDamageMovement_AndLeavesStockUnchanged()
    {
        await using var db = CreateDbContext();
        var product = new Product { Id = Guid.NewGuid(), Name = "Damage Only", Price = 10m, Cost = 7m };
        db.Products.Add(product);
        db.Stocks.Add(new Stock { Id = Guid.NewGuid(), ProductId = product.Id, Quantity = 5m, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var page = new CreateModel(db)
        {
            Input = new CreateModel.DamageInput
            {
                ProductId = product.Id,
                Quantity = 2m,
                Note = "Report only"
            }
        };

        var result = await page.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(5m, (await db.Stocks.SingleAsync(s => s.ProductId == product.Id)).Quantity);
        var movement = Assert.Single(await db.StockMovements.Where(m => m.ProductId == product.Id).ToListAsync());
        Assert.Equal("DAMAGE", movement.MovementType);
        Assert.Equal(-2m, movement.Quantity);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ClearsReturnItemDamage()
    {
        await using var db = CreateDbContext();
        var product = new Product { Id = Guid.NewGuid(), Name = "Return Damage", Price = 10m, Cost = 7m };
        var productReturn = new ProductReturn
        {
            Id = Guid.NewGuid(),
            ReturnNumber = "R-DEL-DAMAGE",
            Status = "Open",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var returnItem = new ProductReturnItem
        {
            Id = Guid.NewGuid(),
            ProductReturnId = productReturn.Id,
            ProductId = product.Id,
            Quantity = 3m,
            DamagedQuantity = 2m,
            IsDamaged = true
        };
        db.Products.Add(product);
        db.ProductReturns.Add(productReturn);
        db.ProductReturnItems.Add(returnItem);
        await db.SaveChangesAsync();

        var page = new IndexModel(db);

        var result = await page.OnPostDeleteAsync(returnItem.Id, IndexModel.DamageSource.ReturnItem, null);

        Assert.IsType<RedirectToPageResult>(result);
        var savedItem = await db.ProductReturnItems.SingleAsync(i => i.Id == returnItem.Id);
        Assert.Equal(0m, savedItem.DamagedQuantity);
        Assert.False(savedItem.IsDamaged);
        Assert.Equal(3m, savedItem.Quantity);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
