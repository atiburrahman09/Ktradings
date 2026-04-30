using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using KTrading.Data;
using KTrading.Pages.Reports;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

public class InventoryReportTests
{
    [Fact]
    public async Task InventoryValueReport_CalculatesTotals()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase("TestDb"));
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // seed
        var p1 = new KTrading.Models.Product { Id = Guid.NewGuid(), Name = "Prod1", Price = 10m, Cost = 6m, CreatedAt = DateTimeOffset.UtcNow };
        var p2 = new KTrading.Models.Product { Id = Guid.NewGuid(), Name = "Prod2", Price = 5m, Cost = 3m, CreatedAt = DateTimeOffset.UtcNow };
        db.Products.AddRange(p1, p2);
        db.Stocks.AddRange(new KTrading.Models.Stock { Id = Guid.NewGuid(), ProductId = p1.Id, Quantity = 10, UpdatedAt = DateTimeOffset.UtcNow },
            new KTrading.Models.Stock { Id = Guid.NewGuid(), ProductId = p2.Id, Quantity = 20, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var model = new InventoryValueModel(db);
        await model.OnGetAsync();

        Assert.Equal(2, model.Rows.Count);
        var row1 = model.Rows.First(r => r.ProductName == "Prod1");
        Assert.Equal(10m, row1.Quantity);
        Assert.Equal(6m, row1.UnitCost);
        Assert.Equal(60m, row1.Value);

        var total = model.TotalValue;
        Assert.Equal(60m + (20m * 3m), total);
    }
}
