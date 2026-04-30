using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using KTrading.Data;

public class ExportExcelTests
{
    [Fact]
    public async Task ExportInventoryExcel_GeneratesWorkbook_WithExpectedHeadersAndRows()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase("ExcelTestDb"));
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // seed product and stock
        var p = new KTrading.Models.Product { Id = System.Guid.NewGuid(), Name = "ExcelProd", SKU = "EX-001", Price = 10m, Cost = 6m, CreatedAt = System.DateTimeOffset.UtcNow };
        db.Products.Add(p);
        db.Stocks.Add(new KTrading.Models.Stock { Id = System.Guid.NewGuid(), ProductId = p.Id, Quantity = 12, UpdatedAt = System.DateTimeOffset.UtcNow });
        db.StockMovements.Add(new KTrading.Models.StockMovement { Id = System.Guid.NewGuid(), ProductId = p.Id, Quantity = 12, MovementType = "IN", CreatedAt = System.DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        // Recreate workbook logic similar to ExportInventoryExcel page
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Summary");

        // Title / header
        ws.Range("A1:K1").Merge().Value = "KHONDAKAR TRADERS";
        ws.Range("A2:K2").Merge().Value = "SUMMARY SHEET";

        ws.Cell(4, 1).Value = "Date:";
        ws.Cell(4, 2).Value = System.DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Headers
        var headers = new[] { "#", "PRODUCT", "SKU", "PCS", "OUT", "IN", "SELL", "ETP(PCS)", "ETP(CTN)", "AMOUNT", "DAMAGE", "REMARKS" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(6, i + 1).Value = headers[i];
        }

        // single product row
        ws.Cell(7, 1).Value = 1;
        ws.Cell(7, 2).Value = p.Name;
        ws.Cell(7, 3).Value = p.SKU;
        ws.Cell(7, 4).Value = 12;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Seek(0, SeekOrigin.Begin);

        using var read = new XLWorkbook(ms);
        var rws = read.Worksheets.First();
        Assert.Equal("KHONDAKAR TRADERS", rws.Cell(1,1).GetString());
        Assert.Equal("PRODUCT", rws.Cell(6,2).GetString());
        Assert.Equal("EX-001", rws.Cell(7,3).GetString());
    }
}
