using ClosedXML.Excel;
using KTrading.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace KTrading.Pages.Reports
{
    public class ExportInventoryExcelModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ExportInventoryExcelModel(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> OnGetAsync(string? categoryName)
        {
            var productsQuery = _db.Products.Include(p => p.ProductCategory).AsQueryable();
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                productsQuery = productsQuery.Where(p => p.ProductCategory != null && p.ProductCategory.Name == categoryName);
            }

            var products = await productsQuery
                .OrderBy(p => p.ProductCategory == null ? "Uncategorized" : p.ProductCategory.Name)
                .ThenBy(p => p.Name)
                .ToListAsync();
            var productIds = products.Select(p => p.Id).ToHashSet();
            var stocks = await _db.Stocks.Where(s => productIds.Contains(s.ProductId)).ToListAsync();
            var movements = await _db.StockMovements.Where(m => productIds.Contains(m.ProductId)).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Summary");

            // Title / header
            ws.Range("A1:L1").Merge().Value = "KHONDAKAR TRADERS";
            ws.Range("A1:L1").Style.Font.Bold = true;
            ws.Range("A2:L2").Merge().Value = string.IsNullOrWhiteSpace(categoryName)
                ? "SUMMARY SHEET"
                : $"{categoryName.ToUpperInvariant()} PRODUCTS SUMMARY SHEET";
            ws.Range("A2:L2").Style.Font.Italic = true;

            ws.Cell(4, 1).Value = "Date:";
            ws.Cell(4, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // Headers
            var headers = new[] { "#", "CATEGORY", "PRODUCT", "SKU", "PCS", "OUT", "IN", "SELL", "ETP(PCS)", "ETP(CTN)", "AMOUNT", "DAMAGE" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(6, i + 1).Value = headers[i];
                ws.Cell(6, i + 1).Style.Font.Bold = true;
                ws.Cell(6, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Cell(6, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = 7;
            foreach (var p in products)
            {
                var stock = stocks.FirstOrDefault(s => s.ProductId == p.Id);
                var qty = stock?.Quantity ?? 0m;
                var ins = movements.Where(m => m.ProductId == p.Id && m.Quantity > 0).Sum(m => m.Quantity);
                var outs = movements.Where(m => m.ProductId == p.Id && m.Quantity < 0).Sum(m => -m.Quantity);
                var damage = movements.Where(m => m.ProductId == p.Id && string.Equals(m.MovementType, "DAMAGE", StringComparison.OrdinalIgnoreCase)).Sum(m => m.Quantity);

                ws.Cell(row, 1).Value = row - 6; // serial
                ws.Cell(row, 2).Value = p.ProductCategory?.Name ?? "Uncategorized";
                ws.Cell(row, 3).Value = p.Name;
                ws.Cell(row, 4).Value = p.SKU;
                ws.Cell(row, 5).Value = qty;
                ws.Cell(row, 6).Value = outs;
                ws.Cell(row, 7).Value = ins;
                ws.Cell(row, 8).Value = 0; // SELL placeholder
                ws.Cell(row, 9).Value = p.Price; // ETP(PCS) use Price
                ws.Cell(row, 10).Value = ""; // ETP(CTN) placeholder
                ws.Cell(row, 11).Value = qty * p.Price; // amount
                ws.Cell(row, 12).Value = damage;

                // format numeric
                ws.Cell(row, 5).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 6).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 7).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 9).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 11).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 12).Style.NumberFormat.Format = "0.00";

                row++;
            }

            // Auto-fit columns
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var reportName = string.IsNullOrWhiteSpace(categoryName) ? "Inventory" : categoryName.Replace(" ", "_");
            var fileName = $"{reportName}_Summary_{DateTime.UtcNow:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
