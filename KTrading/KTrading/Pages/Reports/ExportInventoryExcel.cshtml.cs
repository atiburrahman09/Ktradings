using ClosedXML.Excel;
using KTrading.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Reports
{
    public class ExportInventoryExcelModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public Guid? CategoryId { get; set; }
        private readonly ApplicationDbContext _db;

        public ExportInventoryExcelModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Load products WITHOUT Include(p => p.ProductCategory)
            var query = _db.Products.AsQueryable();

            if (CategoryId.HasValue)
            {
                query = query.Where(p => p.ProductCategoryId == CategoryId);
            }

            var products = await query
                .OrderBy(p => p.Name)
                .ToListAsync();

            var productIds = products.Select(p => p.Id).ToHashSet();

            // Load categories separately and build a lookup (keeps query simpler and avoids Include)
            var categoryIds = products
                .Select(p => p.ProductCategoryId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var categories = await _db.ProductCategories
                .Where(c => categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            var latestStockByProduct = (await _db.Stocks
                    .Where(s => productIds.Contains(s.ProductId))
                    .ToListAsync())
                .GroupBy(s => s.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(s => s.UpdatedAt).First());

            var reportProducts = products
                .Where(p => latestStockByProduct.ContainsKey(p.Id))
                // Order in-memory using category name from lookup (or "Uncategorized")
                .OrderBy(p => p.ProductCategoryId.HasValue && categories.TryGetValue(p.ProductCategoryId.Value, out var n) ? n : "Uncategorized")
                .ThenBy(p => p.Name)
                .ToList();
            var reportProductIds = reportProducts.Select(p => p.Id).ToHashSet();

            var salesItems = (await _db.SalesOrderItems.ToListAsync())
                .Where(i => reportProductIds.Contains(i.ProductId))
                .ToList();
            var matchingSalesOrderIds = salesItems.Select(i => i.SalesOrderId).ToHashSet();
            var productReturns = (await _db.ProductReturns.ToListAsync())
                .Where(r => r.SalesOrderId.HasValue && matchingSalesOrderIds.Contains(r.SalesOrderId.Value))
                .ToList();
            var productReturnIds = productReturns.Select(r => r.Id).ToHashSet();
            var returnItems = (await _db.ProductReturnItems.ToListAsync())
                .Where(i => reportProductIds.Contains(i.ProductId)
                    && productReturnIds.Contains(i.ProductReturnId)
                    && !i.IsOutsideSalesDamageReturn)
                .ToList();
            var returnedQuantityByProduct = returnItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => Math.Max(i.Quantity, 0m)));
            var soldQuantityByProduct = salesItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => Math.Max(g.Sum(i => i.Quantity) - returnedQuantityByProduct.GetValueOrDefault(g.Key), 0m));

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Latest Stock");

            ws.Range("A1:F1").Merge().Value = "KHONDAKAR TRADERS";
            ws.Range("A1:F1").Style.Font.Bold = true;
            ws.Range("A2:F2").Merge().Value = "LATEST STOCK REPORT";
            ws.Range("A2:F2").Style.Font.Italic = true;

            ws.Cell(4, 1).Value = "Date:";
            ws.Cell(4, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var headers = new[] { "PRODUCT CATEGORY", "PRODUCT NAME", "UNIT PRICE", "TOTAL QUANTITY", "TOTAL SELL", "TOTAL REMAINING" };
            for (var i = 0; i < headers.Length; i++)
            {
                ws.Cell(6, i + 1).Value = headers[i];
                ws.Cell(6, i + 1).Style.Font.Bold = true;
                ws.Cell(6, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Cell(6, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            var row = 7;
            foreach (var product in reportProducts)
            {
                var remainingQuantity = latestStockByProduct[product.Id].Quantity;
                var soldQuantity = soldQuantityByProduct.GetValueOrDefault(product.Id);
                var totalQuantity = soldQuantity + remainingQuantity;

                // Resolve category name from lookup (no Include used)
                var categoryName = product.ProductCategoryId.HasValue && categories.TryGetValue(product.ProductCategoryId.Value, out var cn)
                    ? cn
                    : "Uncategorized";

                ws.Cell(row, 1).Value = categoryName;
                ws.Cell(row, 2).Value = product.Name;
                ws.Cell(row, 3).Value = product.Price;
                ws.Cell(row, 4).Value = totalQuantity;
                ws.Cell(row, 5).Value = soldQuantity;
                ws.Cell(row, 6).Value = remainingQuantity;

                for (var col = 3; col <= 6; col++)
                {
                    ws.Cell(row, col).Style.NumberFormat.Format = "0.00";
                }

                row++;
            }

            var totalRow = row;
            ws.Cell(totalRow, 1).Value = "TOTAL";
            _ = ws.Range(totalRow, 1, totalRow, 3).Merge();
            ws.Cell(totalRow, 1).Style.Font.Bold = true;
            ws.Cell(totalRow, 4).FormulaA1 = row > 7 ? $"SUM(D7:D{row - 1})" : "0";
            ws.Cell(totalRow, 5).FormulaA1 = row > 7 ? $"SUM(E7:E{row - 1})" : "0";
            ws.Cell(totalRow, 6).FormulaA1 = row > 7 ? $"SUM(F7:F{row - 1})" : "0";
            ws.Range(totalRow, 1, totalRow, 6).Style.Font.Bold = true;
            ws.Range(totalRow, 4, totalRow, 6).Style.NumberFormat.Format = "0.00";

            ws.Range(6, 1, totalRow, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            _ = ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            _ = ms.Seek(0, SeekOrigin.Begin);

            var fileName = $"Inventory_Latest_Stock_{DateTime.UtcNow:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
