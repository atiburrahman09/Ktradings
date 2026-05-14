using ClosedXML.Excel;
using KTrading.Models;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.SalesOrders
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public IEnumerable<SalesOrder> Orders { get; set; } = Array.Empty<SalesOrder>();
        public Dictionary<Guid, string> CustomerNames { get; set; } = new();
        public Dictionary<Guid, string> SalesOfficerNames { get; set; } = new();
        public IEnumerable<SelectListItem> CustomerList { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> SalesOfficerList { get; set; } = Array.Empty<SelectListItem>();
        public PaginationModel Pager { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid? CustomerId { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid? SalesOfficerId { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DateFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DateTo { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public async Task OnGetAsync()
        {
            const int pageSize = 10;
            PageNumber = Math.Max(PageNumber, 1);

            CustomerNames = await _db.Customers.ToDictionaryAsync(c => c.Id, c => c.Name);
            SalesOfficerNames = await _db.SalesOfficers.ToDictionaryAsync(o => o.Id, o => o.Name);
            CustomerList = CustomerNames
                .OrderBy(c => c.Value)
                .Select(c => new SelectListItem(c.Value, c.Key.ToString()))
                .ToList();
            SalesOfficerList = SalesOfficerNames
                .OrderBy(o => o.Value)
                .Select(o => new SelectListItem(o.Value, o.Key.ToString()))
                .ToList();

            var query = _db.SalesOrders.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var search = SearchTerm.Trim();
                var matchingCustomerIds = CustomerNames
                    .Where(c => c.Value.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Key)
                    .ToList();

                query = query.Where(BuildSalesOrderSearchPredicate(search, matchingCustomerIds));
            }

            if (CustomerId.HasValue)
            {
                query = query.Where(o => o.CustomerId == CustomerId.Value);
            }

            if (SalesOfficerId.HasValue)
            {
                query = query.Where(o => o.SalesOfficerId == SalesOfficerId.Value);
            }

            if (DateFrom.HasValue)
            {
                var from = new DateTimeOffset(DateFrom.Value.Date, TimeSpan.Zero);
                query = query.Where(o => o.OrderDate >= from);
            }

            if (DateTo.HasValue)
            {
                var to = new DateTimeOffset(DateTo.Value.Date.AddDays(1), TimeSpan.Zero);
                query = query.Where(o => o.OrderDate < to);
            }

            var totalItems = await query.CountAsync();
            Pager = new PaginationModel { PageNumber = PageNumber, PageSize = pageSize, TotalItems = totalItems };
            Orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((PageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IActionResult> OnGetPrintAsync(Guid id)
        {
            var order = await _db.SalesOrders.FindAsync(id);
            if (order is null) return NotFound();

            var customer = await _db.Customers.FindAsync(order.CustomerId);
            var salesOfficer = order.SalesOfficerId.HasValue
                ? await _db.SalesOfficers.FindAsync(order.SalesOfficerId.Value)
                : null;
            var items = await _db.SalesOrderItems
                .Where(i => i.SalesOrderId == id)
                .ToListAsync();
            var payments = await _db.Payments
                .Where(p => p.SalesOrderId == id)
                .OrderBy(p => p.PaymentDate)
                .ToListAsync();

            var productIds = items.Select(i => i.ProductId).ToHashSet();
            var products = await _db.Products
                .Include(p => p.ProductCategory)
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);
            var stocks = await _db.Stocks
                .Where(s => productIds.Contains(s.ProductId))
                .ToListAsync();
            var movements = await _db.StockMovements
                .Where(m => productIds.Contains(m.ProductId))
                .ToListAsync();
            var itemGroups = items
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    SoldQuantity = g.Sum(i => i.Quantity),
                    SoldAmount = g.Sum(i => i.LineTotal)
                })
                .ToList();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Sales Order");

            ws.Range("A1:M1").Merge().Value = "KHONDAKAR TRADERS";
            ws.Range("A1:M1").Style.Font.Bold = true;
            ws.Range("A2:M2").Merge().Value = "SALES ORDER";
            ws.Range("A2:M2").Style.Font.Italic = true;

            ws.Cell(4, 1).Value = "Date:";
            ws.Cell(4, 2).Value = order.OrderDate.ToString("yyyy-MM-dd");
            ws.Cell(4, 4).Value = "Order #:";
            ws.Cell(4, 5).Value = order.OrderNumber;
            ws.Cell(5, 1).Value = "Customer:";
            ws.Cell(5, 2).Value = customer?.Name ?? order.CustomerId.ToString();
            ws.Cell(5, 4).Value = "Sales Officer:";
            ws.Cell(5, 5).Value = salesOfficer?.Name ?? string.Empty;
            ws.Range("A4:A5").Style.Font.Bold = true;
            ws.Cell(4, 4).Style.Font.Bold = true;
            ws.Cell(5, 4).Style.Font.Bold = true;

            var headers = new[] { "#", "CATEGORY", "PRODUCT", "SKU", "CURRENT STOCK", "OUT", "IN", "DAMAGE", "SOLD QTY", "UNIT COST", "UNIT SELL PRICE", "STOCK VALUE", "SOLD AMOUNT" };
            for (var i = 0; i < headers.Length; i++)
            {
                ws.Cell(6, i + 1).Value = headers[i];
                ws.Cell(6, i + 1).Style.Font.Bold = true;
                ws.Cell(6, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Cell(6, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            var row = 7;
            foreach (var item in itemGroups)
            {
                products.TryGetValue(item.ProductId, out var product);
                var qty = stocks.FirstOrDefault(s => s.ProductId == item.ProductId)?.Quantity ?? 0m;
                var outs = movements.Where(m => m.ProductId == item.ProductId && m.Quantity < 0).Sum(m => -m.Quantity);
                var ins = movements.Where(m => m.ProductId == item.ProductId && m.Quantity > 0).Sum(m => m.Quantity);
                var damage = movements
                    .Where(m => m.ProductId == item.ProductId
                        && m.ReferenceId == order.Id
                        && string.Equals(m.MovementType, "DAMAGE", StringComparison.OrdinalIgnoreCase))
                    .Sum(m => Math.Abs(m.Quantity));
                var stockValue = qty * (product?.Cost ?? 0m);

                ws.Cell(row, 1).Value = row - 6;
                ws.Cell(row, 2).Value = product?.ProductCategory?.Name ?? "Uncategorized";
                ws.Cell(row, 3).Value = product?.Name ?? item.ProductId.ToString();
                ws.Cell(row, 4).Value = product?.SKU ?? string.Empty;
                ws.Cell(row, 5).Value = qty;
                ws.Cell(row, 6).Value = outs;
                ws.Cell(row, 7).Value = ins;
                ws.Cell(row, 8).Value = damage;
                ws.Cell(row, 9).Value = item.SoldQuantity;
                ws.Cell(row, 10).Value = product?.Cost ?? 0m;
                ws.Cell(row, 11).Value = product?.Price ?? 0m;
                ws.Cell(row, 12).Value = stockValue;
                ws.Cell(row, 13).Value = item.SoldAmount;
                ws.Range(row, 5, row, 13).Style.NumberFormat.Format = "0.00";
                row++;
            }

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
            ws.Range(totalRow, 1, totalRow, 13).Style.Font.Bold = true;
            ws.Range(totalRow, 5, totalRow, 13).Style.NumberFormat.Format = "0.00";

            var summaryRow = totalRow + 3;
            ws.Cell(summaryRow, 11).Value = "Total";
            ws.Cell(summaryRow, 13).Value = order.Total;
            ws.Cell(summaryRow + 1, 11).Value = "Commission";
            ws.Cell(summaryRow + 1, 13).Value = order.Commission;
            ws.Cell(summaryRow + 2, 11).Value = "Khajna";
            ws.Cell(summaryRow + 2, 13).Value = order.Khajna;
            ws.Cell(summaryRow + 3, 11).Value = "DSR Salary";
            ws.Cell(summaryRow + 3, 13).Value = order.DsrSalary;
            ws.Cell(summaryRow + 4, 11).Value = "Due";
            ws.Cell(summaryRow + 4, 13).Value = order.DueAmount;
            ws.Cell(summaryRow + 5, 11).Value = "Net Total";
            ws.Cell(summaryRow + 5, 13).Value = order.Total - order.Commission - order.Khajna - order.DsrSalary;
            ws.Range(summaryRow, 11, summaryRow + 5, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(summaryRow, 11, summaryRow + 5, 11).Style.Font.Bold = true;
            ws.Range(summaryRow, 13, summaryRow + 5, 13).Style.NumberFormat.Format = "0.00";
            ws.Range(summaryRow + 5, 11, summaryRow + 5, 13).Style.Font.Bold = true;

            var paymentsStartRow = summaryRow + 11;
            ws.Cell(paymentsStartRow, 1).Value = "PAYMENTS";
            ws.Cell(paymentsStartRow, 1).Style.Font.Bold = true;
            ws.Cell(paymentsStartRow + 1, 1).Value = "DATE";
            ws.Cell(paymentsStartRow + 1, 2).Value = "REFERENCE";
            ws.Cell(paymentsStartRow + 1, 3).Value = "AMOUNT";
            ws.Range(paymentsStartRow + 1, 1, paymentsStartRow + 1, 3).Style.Font.Bold = true;
            ws.Range(paymentsStartRow + 1, 1, paymentsStartRow + 1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            for (var col = 1; col <= 3; col++)
            {
                ws.Cell(paymentsStartRow + 1, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            var paymentRow = paymentsStartRow + 2;
            foreach (var payment in payments)
            {
                ws.Cell(paymentRow, 1).Value = payment.PaymentDate.ToString("yyyy-MM-dd");
                ws.Cell(paymentRow, 2).Value = payment.Reference ?? string.Empty;
                ws.Cell(paymentRow, 3).Value = payment.Amount;
                ws.Cell(paymentRow, 3).Style.NumberFormat.Format = "0.00";
                paymentRow++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var safeOrderNumber = string.Join("_", order.OrderNumber.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            var fileName = $"SalesOrder_{safeOrderNumber}_{DateTime.UtcNow:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static Expression<Func<SalesOrder, bool>> BuildSalesOrderSearchPredicate(string search, IEnumerable<Guid> matchingCustomerIds)
        {
            var order = Expression.Parameter(typeof(SalesOrder), "o");
            var orderNumber = Expression.Property(order, nameof(SalesOrder.OrderNumber));
            var contains = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
            Expression body = Expression.Call(orderNumber, contains, Expression.Constant(search));

            foreach (var customerId in matchingCustomerIds)
            {
                var customerMatch = Expression.Equal(
                    Expression.Property(order, nameof(SalesOrder.CustomerId)),
                    Expression.Constant(customerId));
                body = Expression.OrElse(body, customerMatch);
            }

            return Expression.Lambda<Func<SalesOrder, bool>>(body, order);
        }
    }
}
