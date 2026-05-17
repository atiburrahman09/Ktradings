using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using KTrading.Data;
using KTrading.Models;
using KTrading.Pages.ProductReturns;

public class ReturnProcessingTests
{
    [Fact]
    public async Task ProcessReturn_ShouldCreateMovements_And_UpdateStock_And_Status()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase("ReturnTestDb"));
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // seed product and stock
        var product = new Product { Id = Guid.NewGuid(), Name = "TestProduct", Price = 10m, Cost = 6m, CreatedAt = DateTimeOffset.UtcNow };
        db.Products.Add(product);
        var stock = new Stock { Id = Guid.NewGuid(), ProductId = product.Id, Quantity = 5, UpdatedAt = DateTimeOffset.UtcNow };
        db.Stocks.Add(stock);

        // seed return with two items: one damaged, one not
        var ret = new ProductReturn { Id = Guid.NewGuid(), ReturnNumber = "R-T1", CreatedAt = DateTimeOffset.UtcNow, Status = "Open" };
        db.ProductReturns.Add(ret);
        var itemGood = new ProductReturnItem { Id = Guid.NewGuid(), ProductReturnId = ret.Id, ProductId = product.Id, Quantity = 2, IsDamaged = false, Notes = "OK" };
        var itemDam = new ProductReturnItem { Id = Guid.NewGuid(), ProductReturnId = ret.Id, ProductId = product.Id, Quantity = 1, IsDamaged = true, Notes = "Broken" };
        db.ProductReturnItems.AddRange(itemGood, itemDam);
        await db.SaveChangesAsync();

        var pageModel = new DetailsModel(db);

        // act
        var result = await pageModel.OnPostProcessAsync(ret.Id);

        // reload
        var movements = await db.StockMovements.Where(s => s.ReferenceId == ret.Id).ToListAsync();
        var updatedStock = await db.Stocks.FirstAsync(s => s.ProductId == product.Id);
        var updatedReturn = await db.ProductReturns.FindAsync(ret.Id);

        // asserts
        // Should restock all returned quantity. Damaged quantity is only an amount/reporting marker
        // through ProductReturnItem, and does not create a separate stock movement.
        Assert.Contains(movements, m => m.MovementType == "RETURN" && m.Quantity == 2m);
        Assert.Contains(movements, m => m.MovementType == "RETURN" && m.Quantity == 1m);
        Assert.DoesNotContain(movements, m => m.MovementType == "DAMAGE");

        // Stock increased by the full returned quantity (2 + 1)
        Assert.Equal(8m, updatedStock.Quantity);

        // Return status should be Processed
        Assert.Equal("Processed", updatedReturn.Status);
    }
}
