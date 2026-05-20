using ClosedXML.Excel;
using KTrading.Data;
using KTrading.Models;
using KTrading.Services;
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
            var stocks = (await _db.Stocks.ToListAsync())
                .Where(s => productIds.Contains(s.ProductId))
                .ToList();
            var movements = (await _db.StockMovements.ToListAsync())
                .Where(m => productIds.Contains(m.ProductId))
                .ToList();
            var salesItems = (await _db.SalesOrderItems.ToListAsync())
                .Where(i => productIds.Contains(i.ProductId))
                .ToList();
            var matchingSalesOrderIds = salesItems.Select(i => i.SalesOrderId).ToHashSet();
            var salesOrders = (await _db.SalesOrders.ToListAsync())
                .Where(o => matchingSalesOrderIds.Contains(o.Id))
                .ToList();
            var productReturns = (await _db.ProductReturns.ToListAsync())
                .Where(r => r.SalesOrderId.HasValue && matchingSalesOrderIds.Contains(r.SalesOrderId.Value))
                .ToList();
            var productReturnIds = productReturns.Select(r => r.Id).ToHashSet();
            var returnSalesOrderIds = productReturns.ToDictionary(r => r.Id, r => r.SalesOrderId!.Value);
            var returnItems = (await _db.ProductReturnItems.ToListAsync())
                .Where(i => productIds.Contains(i.ProductId) && productReturnIds.Contains(i.ProductReturnId) && !i.IsOutsideSalesDamageReturn)
                .ToList();
            var reportDateStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
            var reportDateEnd = reportDateStart.AddDays(1);
            var todaySalesOrderIds = (await _db.SalesOrders.ToListAsync())
                .Where(o => o.OrderDate >= reportDateStart && o.OrderDate < reportDateEnd)
                .Select(o => o.Id)
                .ToHashSet();
            var todaySalesProductIds = (await _db.SalesOrderItems.ToListAsync())
                .Where(i => todaySalesOrderIds.Contains(i.SalesOrderId))
                .Select(i => i.ProductId)
                .ToHashSet();
            var outsideDamageReturnIds = (await _db.ProductReturns.ToListAsync())
                .Where(r => r.CreatedAt >= reportDateStart && r.CreatedAt < reportDateEnd)
                .Select(r => r.Id)
                .ToHashSet();
            var outsideDamageItems = (await _db.ProductReturnItems.ToListAsync())
                .Where(i => i.IsOutsideSalesDamageReturn
                    && productIds.Contains(i.ProductId)
                    && outsideDamageReturnIds.Contains(i.ProductReturnId)
                    && !todaySalesProductIds.Contains(i.ProductId))
                .ToList();
            var productPrices = products.ToDictionary(p => p.Id, p => p.Price);
            var outsideSalesDamageReturn = outsideDamageItems.Sum(i => GetDamagedReturnQuantity(i) * productPrices.GetValueOrDefault(i.ProductId));
            var salesUnitPrices = salesItems
                .GroupBy(i => new { i.SalesOrderId, i.ProductId })
                .ToDictionary(
                    g => (g.Key.SalesOrderId, g.Key.ProductId),
                    g => g.Sum(i => i.Quantity) == 0 ? 0 : g.Sum(i => i.LineTotal) / g.Sum(i => i.Quantity));
            var returnAmounts = returnItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(i =>
                    {
                        var salesOrderId = returnSalesOrderIds[i.ProductReturnId];
                        var unitPrice = salesUnitPrices.GetValueOrDefault((salesOrderId, i.ProductId));
                        return GetSalesAdjustmentQuantity(i) * unitPrice;
                    }));
            var returnGroups = returnItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        ReturnedQuantity = g.Sum(i => i.Quantity),
                        DamagedQuantity = g.Sum(GetDamagedReturnQuantity)
                    });
            var grandReturnAmount = returnAmounts.Sum(a => a.Value);

            var grandSalesTotal = Math.Max(salesItems.Sum(i => i.LineTotal) - grandReturnAmount, 0m);
            var grandCommission = salesOrders.Sum(o => o.Commission);
            var grandKhajna = salesOrders.Sum(o => o.Khajna);
            var grandDsrSalary = salesOrders.Sum(o => o.DsrSalary);
            var grandOtherCosting = salesOrders.Sum(o => o.OtherCosting);
            var grandDue = salesOrders.Sum(o => o.DueAmount);
            var grandDamageAmount = outsideSalesDamageReturn;

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Summary");

            // Title / header
            ws.Range("A1:N1").Merge().Value = "KHONDAKAR TRADERS";
            ws.Range("A1:N1").Style.Font.Bold = true;
            ws.Range("A2:N2").Merge().Value = string.IsNullOrWhiteSpace(categoryName)
                ? "SUMMARY SHEET"
                : $"{categoryName.ToUpperInvariant()} PRODUCTS SUMMARY SHEET";
            ws.Range("A2:N2").Style.Font.Italic = true;

            ws.Cell(4, 1).Value = "Date:";
            ws.Cell(4, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // Headers
            var headers = new[] { "#", "CATEGORY", "PRODUCT", "SKU", "CURRENT STOCK", "IN", "OUT", "DAMAGE", "SOLD QTY", "UNIT COST", "UNIT SELL PRICE", "STOCK VALUE", "SOLD AMOUNT", "DAMAGE AMOUNT" };
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
                returnGroups.TryGetValue(p.Id, out var returnGroup);
                var returnedQuantity = returnGroup?.ReturnedQuantity ?? 0m;
                var damagedReturnQuantity = returnGroup?.DamagedQuantity ?? 0m;
                var damageMovementQuantity = movements
                    .Where(m => m.ProductId == p.Id && string.Equals(m.MovementType, "DAMAGE", StringComparison.OrdinalIgnoreCase))
                    .Sum(m => Math.Abs(m.Quantity));
                var damage = damagedReturnQuantity + damageMovementQuantity;
                var soldQty = Math.Max(salesItems.Where(i => i.ProductId == p.Id).Sum(i => i.Quantity) - returnedQuantity, 0m);
                var soldAmount = Math.Max(salesItems.Where(i => i.ProductId == p.Id).Sum(i => i.LineTotal) - returnAmounts.GetValueOrDefault(p.Id), 0m);
                var stockValue = qty * p.Cost;
                var damageAmount = damage * p.Cost;
                grandDamageAmount += damageAmount;

                ws.Cell(row, 1).Value = row - 6; // serial
                ws.Cell(row, 2).Value = p.ProductCategory?.Name ?? "Uncategorized";
                ws.Cell(row, 3).Value = p.Name;
                ws.Cell(row, 4).Value = p.SKU;
                ws.Cell(row, 5).Value = qty;
                ws.Cell(row, 6).Value = ins;
                ws.Cell(row, 7).Value = outs;
                ws.Cell(row, 8).Value = damage;
                ws.Cell(row, 9).Value = soldQty;
                ws.Cell(row, 10).Value = p.Cost;
                ws.Cell(row, 11).Value = p.Price;
                ws.Cell(row, 12).Value = stockValue;
                ws.Cell(row, 13).Value = soldAmount;
                ws.Cell(row, 14).Value = damageAmount;

                // format numeric
                for (int col = 5; col <= 14; col++)
                {
                    ws.Cell(row, col).Style.NumberFormat.Format = "0.00";
                }

                row++;
            }

            var grandNetTotal = SalesOrderFinancials.CalculateNetAmount(grandSalesTotal, grandCommission, grandDsrSalary, grandDamageAmount, grandOtherCosting);
            var grandPaidAmount = SalesOrderFinancials.CalculatePaidAmount(grandSalesTotal, grandCommission, grandDsrSalary, grandDamageAmount, grandOtherCosting, grandDue);

            var totalRow = row;
            ws.Cell(totalRow, 1).Value = "TOTAL";
            ws.Range(totalRow, 1, totalRow, 4).Merge();
            ws.Cell(totalRow, 1).Style.Font.Bold = true;
            ws.Cell(totalRow, 5).FormulaA1 = $"SUM(E7:E{row - 1})";
            ws.Cell(totalRow, 6).FormulaA1 = $"SUM(F7:F{row - 1})";
            ws.Cell(totalRow, 7).FormulaA1 = $"SUM(G7:G{row - 1})";
            ws.Cell(totalRow, 8).FormulaA1 = $"SUM(H7:H{row - 1})";
            ws.Cell(totalRow, 9).FormulaA1 = $"SUM(I7:I{row - 1})";
            ws.Cell(totalRow, 12).FormulaA1 = $"SUM(L7:L{row - 1})";
            ws.Cell(totalRow, 13).FormulaA1 = $"SUM(M7:M{row - 1})";
            ws.Cell(totalRow, 14).FormulaA1 = $"SUM(N7:N{row - 1})";
            ws.Range(totalRow, 1, totalRow, 14).Style.Font.Bold = true;
            ws.Range(totalRow, 5, totalRow, 14).Style.NumberFormat.Format = "0.00";

            var summaryRow = totalRow + 3;
            ws.Cell(summaryRow, 11).Value = "Total";
            ws.Cell(summaryRow, 13).Value = grandSalesTotal;
            ws.Cell(summaryRow + 1, 11).Value = "Commission";
            ws.Cell(summaryRow + 1, 13).Value = grandCommission;
            ws.Cell(summaryRow + 2, 11).Value = "Khajna";
            ws.Cell(summaryRow + 2, 13).Value = grandKhajna;
            ws.Cell(summaryRow + 3, 11).Value = "DSR Salary";
            ws.Cell(summaryRow + 3, 13).Value = grandDsrSalary;
            ws.Cell(summaryRow + 4, 11).Value = "Damage Amount";
            ws.Cell(summaryRow + 4, 13).Value = grandDamageAmount;
            ws.Cell(summaryRow + 5, 11).Value = "Other Costing";
            ws.Cell(summaryRow + 5, 13).Value = grandOtherCosting;
            ws.Cell(summaryRow + 6, 11).Value = "Due";
            ws.Cell(summaryRow + 6, 13).Value = grandDue;
            ws.Cell(summaryRow + 7, 11).Value = "Paid Amount";
            ws.Cell(summaryRow + 7, 13).Value = grandPaidAmount;
            ws.Cell(summaryRow + 8, 11).Value = "Net Total";
            ws.Cell(summaryRow + 8, 13).Value = grandNetTotal;
            ws.Range(summaryRow, 11, summaryRow + 8, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(summaryRow, 11, summaryRow + 8, 11).Style.Font.Bold = true;
            ws.Range(summaryRow, 13, summaryRow + 8, 13).Style.NumberFormat.Format = "0.00";
            ws.Range(summaryRow + 7, 11, summaryRow + 8, 13).Style.Font.Bold = true;

            // Auto-fit columns
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var reportName = string.IsNullOrWhiteSpace(categoryName) ? "Inventory" : categoryName.Replace(" ", "_");
            var fileName = $"{reportName}_Summary_{DateTime.UtcNow:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static decimal GetSalesAdjustmentQuantity(ProductReturnItem item)
        {
            return Math.Max(item.Quantity, 0m);
        }

        private static decimal GetDamagedReturnQuantity(ProductReturnItem item)
        {
            return item.DamagedQuantity > 0 ? item.DamagedQuantity : item.IsDamaged ? item.Quantity : 0m;
        }
    }
}
