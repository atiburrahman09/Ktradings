using ClosedXML.Excel;
using KTrading.Data;
using KTrading.Models;
using KTrading.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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
        public Dictionary<Guid, decimal> AdjustedTotals { get; set; } = new();
        public Dictionary<Guid, decimal> AdjustedPaidAmounts { get; set; } = new();
        public Dictionary<Guid, decimal> AdjustedDues { get; set; } = new();
        public Dictionary<Guid, decimal> DamageAmounts { get; set; } = new();
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
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((PageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            Orders = orders;

            await LoadAdjustedOrderAmountsAsync(orders);
        }

        private async Task LoadAdjustedOrderAmountsAsync(IReadOnlyCollection<SalesOrder> orders)
        {
            var orderIds = orders.Select(o => o.Id).ToHashSet();
            AdjustedTotals = orders.ToDictionary(o => o.Id, o => o.Total);
            AdjustedDues = orders.ToDictionary(o => o.Id, o => Math.Max(o.DueAmount, 0m));
            DamageAmounts = orders.ToDictionary(o => o.Id, _ => 0m);
            AdjustedPaidAmounts = orders.ToDictionary(o => o.Id, o => SalesOrderFinancials.CalculatePaidAmount(
                AdjustedTotals[o.Id],
                o.Commission,
                o.DsrSalary,
                DamageAmounts[o.Id],
                o.OtherCosting,
                AdjustedDues[o.Id], o.Khajna)
            );

            if (!orderIds.Any())
            {
                return;
            }

            var salesItems = await _db.SalesOrderItems
                .Where(i => orderIds.Contains(i.SalesOrderId))
                .ToListAsync();
            var productReturns = await _db.ProductReturns
                .Where(r => r.SalesOrderId.HasValue && orderIds.Contains(r.SalesOrderId.Value))
                .ToListAsync();
            var productReturnIds = productReturns.Select(r => r.Id).ToHashSet();

            var salesUnitPrices = salesItems
                .GroupBy(i => new { i.SalesOrderId, i.ProductId })
                .ToDictionary(
                    g => (g.Key.SalesOrderId, g.Key.ProductId),
                    g => g.Sum(i => i.Quantity) == 0 ? 0 : g.Sum(i => i.LineTotal) / g.Sum(i => i.Quantity));
            var returnedAmounts = new Dictionary<Guid, decimal>();
            var damageAmounts = new Dictionary<Guid, decimal>();

            if (productReturnIds.Any())
            {
                var returnSalesOrderIds = productReturns.ToDictionary(r => r.Id, r => r.SalesOrderId!.Value);
                var returnItems = await _db.ProductReturnItems
                    .Where(i => productReturnIds.Contains(i.ProductReturnId))
                    .Where(i => !i.IsOutsideSalesDamageReturn)
                    .ToListAsync();
                returnedAmounts = returnItems
                    .GroupBy(i => returnSalesOrderIds[i.ProductReturnId])
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(i =>
                        {
                            var salesOrderId = returnSalesOrderIds[i.ProductReturnId];
                            var unitPrice = salesUnitPrices.GetValueOrDefault((salesOrderId, i.ProductId));
                            return GetSalesAdjustmentQuantity(i) * unitPrice;
                        }));
                damageAmounts = returnItems
                    .GroupBy(i => returnSalesOrderIds[i.ProductReturnId])
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(i =>
                        {
                            var salesOrderId = returnSalesOrderIds[i.ProductReturnId];
                            var unitPrice = salesUnitPrices.GetValueOrDefault((salesOrderId, i.ProductId));
                            return GetDamagedReturnQuantity(i) * unitPrice;
                        }));
            }

            var outsideDamageAmounts = await CalculateOutsideSalesDamageAmountsAsync(orders);

            foreach (var order in orders)
            {
                var returnedAmount = returnedAmounts.GetValueOrDefault(order.Id);
                AdjustedTotals[order.Id] = Math.Max(order.Total - returnedAmount, 0m);
                AdjustedDues[order.Id] = Math.Max(order.DueAmount, 0m);
                DamageAmounts[order.Id] = damageAmounts.GetValueOrDefault(order.Id) + outsideDamageAmounts.GetValueOrDefault(order.Id);
                AdjustedPaidAmounts[order.Id] = SalesOrderFinancials.CalculatePaidAmount(
                    AdjustedTotals[order.Id],
                    order.Commission,
                    order.DsrSalary,
                    DamageAmounts[order.Id],
                    order.OtherCosting,
                    AdjustedDues[order.Id], order.Khajna);
            }
        }

        private async Task<Dictionary<Guid, decimal>> CalculateOutsideSalesDamageAmountsAsync(
            IReadOnlyCollection<SalesOrder> orders)
        {
            var outsideDamageAmounts = orders.ToDictionary(o => o.Id, _ => 0m);
            var orderIds = orders.Select(o => o.Id).ToHashSet();
            var outsideDamageRows = await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId.HasValue && orderIds.Contains(r.SalesOrderId.Value)),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => new { Item = item, SalesOrderId = ret.SalesOrderId!.Value })
                .Where(r => r.Item.IsOutsideSalesDamageReturn)
                .ToListAsync();
            var outsideDamageProductIds = outsideDamageRows.Select(r => r.Item.ProductId).Distinct().ToList();
            var outsideDamagePrices = outsideDamageProductIds.Any()
                ? await _db.Products
                    .Where(p => outsideDamageProductIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Price)
                : new Dictionary<Guid, decimal>();

            foreach (var group in outsideDamageRows.GroupBy(r => r.SalesOrderId))
            {
                outsideDamageAmounts[group.Key] = group.Sum(r => GetDamagedReturnQuantity(r.Item) * outsideDamagePrices.GetValueOrDefault(r.Item.ProductId));
            }

            return outsideDamageAmounts;
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
            var returnItems = await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == id),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => item)
                .Where(i => productIds.Contains(i.ProductId) && !i.IsOutsideSalesDamageReturn)
                .ToListAsync();
            var outsideDamageItems = await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == id),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => item)
                .Where(i => i.IsOutsideSalesDamageReturn)
                .ToListAsync();
            var outsideDamageProductIds = outsideDamageItems.Select(i => i.ProductId).Distinct().ToList();
            var outsideOnlyProductIds = outsideDamageProductIds.Where(productId => !productIds.Contains(productId)).ToList();
            if (outsideOnlyProductIds.Any())
            {
                stocks.AddRange(await _db.Stocks
                    .Where(s => outsideOnlyProductIds.Contains(s.ProductId))
                    .ToListAsync());
                movements.AddRange(await _db.StockMovements
                    .Where(m => outsideOnlyProductIds.Contains(m.ProductId))
                    .ToListAsync());
            }
            var outsideDamagePrices = outsideDamageProductIds.Any()
                ? await _db.Products
                    .Where(p => outsideDamageProductIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Price)
                : new Dictionary<Guid, decimal>();
            var outsideDamageProducts = outsideDamageProductIds.Any()
                ? await _db.Products
                    .Include(p => p.ProductCategory)
                    .Where(p => outsideDamageProductIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id)
                : new Dictionary<Guid, Product>();
            var outsideDamageGroups = outsideDamageItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        DamagedQuantity = g.Sum(GetDamagedReturnQuantity),
                        DamageAmount = g.Sum(i => GetDamagedReturnQuantity(i) * outsideDamagePrices.GetValueOrDefault(i.ProductId))
                    });
            var itemGroups = items
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    SoldQuantity = g.Sum(i => i.Quantity),
                    SoldAmount = g.Sum(i => i.LineTotal),
                    UnitPrice = g.Sum(i => i.Quantity) == 0 ? 0 : g.Sum(i => i.LineTotal) / g.Sum(i => i.Quantity)
                })
                .ToList();
            var returnGroups = returnItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        ReturnedQuantity = g.Sum(i => i.Quantity),
                        SalesAdjustmentQuantity = g.Sum(GetSalesAdjustmentQuantity),
                        DamagedQuantity = g.Sum(GetDamagedReturnQuantity)
                    });
            var returnedAmountTotal = itemGroups.Sum(i =>
                returnGroups.GetValueOrDefault(i.ProductId)?.SalesAdjustmentQuantity * i.UnitPrice ?? 0m);
            var damageAmountTotal = itemGroups.Sum(i =>
                (returnGroups.GetValueOrDefault(i.ProductId)?.DamagedQuantity * i.UnitPrice ?? 0m)
                + (outsideDamageGroups.GetValueOrDefault(i.ProductId)?.DamageAmount ?? 0m))
                + outsideDamageGroups
                    .Where(g => !productIds.Contains(g.Key))
                    .Sum(g => g.Value.DamageAmount);
            var reportTotal = Math.Max(order.Total - returnedAmountTotal, 0m);
            var reportDue = Math.Max(order.DueAmount, 0m);
            var reportNet = SalesOrderFinancials.CalculateNetAmount(reportTotal, order.Commission, order.DsrSalary, damageAmountTotal, order.OtherCosting, order.Khajna);
            var reportPaid = SalesOrderFinancials.CalculatePaidAmount(reportTotal, order.Commission, order.DsrSalary, damageAmountTotal, order.OtherCosting, reportDue, order.Khajna);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Sales Order");

            ws.Range("A1:N1").Merge().Value = "KHONDAKAR TRADERS";
            ws.Range("A1:N1").Style.Font.Bold = true;
            ws.Range("A2:N2").Merge().Value = "SALES ORDER";
            ws.Range("A2:N2").Style.Font.Italic = true;

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

            var headers = new[] { "#", "CATEGORY", "PRODUCT", "SKU", "CURRENT STOCK", "OUT", "IN", "DAMAGE", "SOLD QTY", "UNIT COST", "UNIT SELL PRICE", "STOCK VALUE", "SOLD AMOUNT", "DAMAGE AMOUNT" };
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
                _ = products.TryGetValue(item.ProductId, out var product);
                var qty = stocks.FirstOrDefault(s => s.ProductId == item.ProductId)?.Quantity ?? 0m;
                var outs = item.SoldQuantity;
                _ = returnGroups.TryGetValue(item.ProductId, out var returnGroup);
                _ = outsideDamageGroups.TryGetValue(item.ProductId, out var outsideDamageGroup);
                var ins = returnGroup?.SalesAdjustmentQuantity ?? 0m;
                var returnedQuantity = returnGroup?.ReturnedQuantity ?? 0m;
                var salesAdjustmentQuantity = returnGroup?.SalesAdjustmentQuantity ?? 0m;
                var damage = (returnGroup?.DamagedQuantity ?? 0m) + (outsideDamageGroup?.DamagedQuantity ?? 0m);
                var netSoldQuantity = Math.Max(item.SoldQuantity - returnedQuantity, 0m);
                var netSoldAmount = Math.Max(item.SoldAmount - (salesAdjustmentQuantity * item.UnitPrice), 0m);
                var stockValue = qty * (product?.Cost ?? 0m);
                var damageAmount = ((returnGroup?.DamagedQuantity ?? 0m) * item.UnitPrice) + (outsideDamageGroup?.DamageAmount ?? 0m);

                ws.Cell(row, 1).Value = row - 6;
                ws.Cell(row, 2).Value = product?.ProductCategory?.Name ?? "Uncategorized";
                ws.Cell(row, 3).Value = product?.Name ?? item.ProductId.ToString();
                ws.Cell(row, 4).Value = product?.SKU ?? string.Empty;
                ws.Cell(row, 5).Value = qty;
                ws.Cell(row, 6).Value = outs;
                ws.Cell(row, 7).Value = ins;
                ws.Cell(row, 8).Value = damage;
                ws.Cell(row, 9).Value = netSoldQuantity;
                ws.Cell(row, 10).Value = product?.Cost ?? 0m;
                ws.Cell(row, 11).Value = product?.Price ?? 0m;
                ws.Cell(row, 12).Value = stockValue;
                ws.Cell(row, 13).Value = netSoldAmount;
                ws.Cell(row, 14).Value = damageAmount;
                ws.Range(row, 5, row, 14).Style.NumberFormat.Format = "0.00";
                row++;
            }

            foreach (var outsideGroup in outsideDamageGroups.Where(g => !productIds.Contains(g.Key)))
            {
                _ = outsideDamageProducts.TryGetValue(outsideGroup.Key, out var product);
                var qty = stocks.FirstOrDefault(s => s.ProductId == outsideGroup.Key)?.Quantity ?? 0m;
                var outs = 0m;
                var ins = 0m;
                var stockValue = qty * (product?.Cost ?? 0m);

                ws.Cell(row, 1).Value = row - 6;
                ws.Cell(row, 2).Value = product?.ProductCategory?.Name ?? "Uncategorized";
                ws.Cell(row, 3).Value = product?.Name ?? outsideGroup.Key.ToString();
                ws.Cell(row, 4).Value = product?.SKU ?? string.Empty;
                ws.Cell(row, 5).Value = qty;
                ws.Cell(row, 6).Value = outs;
                ws.Cell(row, 7).Value = ins;
                ws.Cell(row, 8).Value = outsideGroup.Value.DamagedQuantity;
                ws.Cell(row, 9).Value = 0;
                ws.Cell(row, 10).Value = product?.Cost ?? 0m;
                ws.Cell(row, 11).Value = product?.Price ?? 0m;
                ws.Cell(row, 12).Value = stockValue;
                ws.Cell(row, 13).Value = 0;
                ws.Cell(row, 14).Value = outsideGroup.Value.DamageAmount;
                ws.Range(row, 5, row, 14).Style.NumberFormat.Format = "0.00";
                row++;
            }

            var totalRow = row;
            ws.Cell(totalRow, 1).Value = "TOTAL";
            _ = ws.Range(totalRow, 1, totalRow, 4).Merge();
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
            ws.Cell(summaryRow, 13).Value = reportTotal;
            ws.Cell(summaryRow + 1, 11).Value = "Commission";
            ws.Cell(summaryRow + 1, 13).Value = order.Commission;
            ws.Cell(summaryRow + 2, 11).Value = "Khajna";
            ws.Cell(summaryRow + 2, 13).Value = order.Khajna;
            ws.Cell(summaryRow + 3, 11).Value = "DSR Salary";
            ws.Cell(summaryRow + 3, 13).Value = order.DsrSalary;
            ws.Cell(summaryRow + 4, 11).Value = "Damage Amount";
            ws.Cell(summaryRow + 4, 13).Value = damageAmountTotal;
            ws.Cell(summaryRow + 5, 11).Value = "Other Costing";
            ws.Cell(summaryRow + 5, 13).Value = order.OtherCosting;
            ws.Cell(summaryRow + 6, 11).Value = "Due";
            ws.Cell(summaryRow + 6, 13).Value = reportDue;
            ws.Cell(summaryRow + 7, 11).Value = "Paid Amount";
            ws.Cell(summaryRow + 7, 13).Value = reportPaid;
            ws.Cell(summaryRow + 8, 11).Value = "Net Total";
            ws.Cell(summaryRow + 8, 13).Value = reportNet;
            ws.Range(summaryRow, 11, summaryRow + 8, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(summaryRow, 11, summaryRow + 8, 11).Style.Font.Bold = true;
            ws.Range(summaryRow, 13, summaryRow + 8, 13).Style.NumberFormat.Format = "0.00";
            ws.Range(summaryRow + 7, 11, summaryRow + 8, 13).Style.Font.Bold = true;

            if (!string.IsNullOrWhiteSpace(order.OtherCostingNote))
            {
                ws.Cell(summaryRow + 9, 11).Value = "Other Costing Note";
                ws.Cell(summaryRow + 9, 11).Style.Font.Bold = true;
                ws.Cell(summaryRow + 9, 12).Value = order.OtherCostingNote;
                _ = ws.Range(summaryRow + 9, 12, summaryRow + 9, 13).Merge();
                ws.Range(summaryRow + 9, 11, summaryRow + 9, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            var paymentsStartRow = totalRow + 2;

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
            var initialPayment = payments.FirstOrDefault(IsInitialPayment);
            var otherPaymentsTotal = payments
                .Where(p => p.Id != initialPayment?.Id)
                .Sum(p => p.Amount);
            var adjustedInitialPayment = Math.Max(reportPaid - otherPaymentsTotal, 0m);

            foreach (var payment in payments.Where(p => !IsInitialPayment(p) || adjustedInitialPayment > 0m))
            {
                ws.Cell(paymentRow, 1).Value = payment.PaymentDate.ToString("yyyy-MM-dd");
                ws.Cell(paymentRow, 2).Value = payment.Reference ?? string.Empty;
                ws.Cell(paymentRow, 3).Value = IsInitialPayment(payment) ? adjustedInitialPayment : payment.Amount;
                ws.Cell(paymentRow, 3).Style.NumberFormat.Format = "0.00";
                paymentRow++;
            }

            _ = ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            _ = ms.Seek(0, SeekOrigin.Begin);

            var safeOrderNumber = string.Join("_", order.OrderNumber.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            var fileName = $"SalesOrder_{safeOrderNumber}_{DateTime.UtcNow:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        public async Task<IActionResult> OnPostDeleteAsync(
            Guid id,
            string? searchTerm,
            Guid? customerId,
            Guid? salesOfficerId,
            DateTime? dateFrom,
            DateTime? dateTo,
            int pageNumber = 1)
        {
            var order = await _db.SalesOrders.FindAsync(id);
            if (order is null)
            {
                return NotFound();
            }

            await DeleteSalesOrderAsync(id);

            return RedirectToPage(new
            {
                searchTerm,
                customerId,
                salesOfficerId,
                dateFrom = dateFrom?.ToString("yyyy-MM-dd"),
                dateTo = dateTo?.ToString("yyyy-MM-dd"),
                pageNumber
            });
        }

        private async Task DeleteSalesOrderAsync(Guid salesOrderId)
        {
            var returns = await _db.ProductReturns
                .Where(r => r.SalesOrderId == salesOrderId)
                .ToListAsync();
            var returnIds = returns.Select(r => r.Id).ToHashSet();

            var salesOrderMovements = await _db.StockMovements
                .Where(m => m.ReferenceId == salesOrderId)
                .ToListAsync();
            var returnMovements = returnIds.Any()
                ? await _db.StockMovements
                    .Where(m => m.ReferenceId.HasValue && returnIds.Contains(m.ReferenceId.Value))
                    .ToListAsync()
                : new List<StockMovement>();

            foreach (var movement in salesOrderMovements.Concat(returnMovements)
                .Where(m => !string.Equals(m.MovementType, "DAMAGE", StringComparison.OrdinalIgnoreCase)))
            {
                await ApplyStockDeltaAsync(movement.ProductId, -movement.Quantity);
            }

            _db.StockMovements.RemoveRange(salesOrderMovements);
            _db.StockMovements.RemoveRange(returnMovements);

            if (returnIds.Any())
            {
                var returnItems = await _db.ProductReturnItems
                    .Where(i => returnIds.Contains(i.ProductReturnId))
                    .ToListAsync();
                _db.ProductReturnItems.RemoveRange(returnItems);
                _db.ProductReturns.RemoveRange(returns);
            }

            var payments = await _db.Payments
                .Where(p => p.SalesOrderId == salesOrderId)
                .ToListAsync();
            var items = await _db.SalesOrderItems
                .Where(i => i.SalesOrderId == salesOrderId)
                .ToListAsync();
            var order = await _db.SalesOrders.FindAsync(salesOrderId);

            _db.Payments.RemoveRange(payments);
            _db.SalesOrderItems.RemoveRange(items);
            if (order is not null)
            {
                _ = _db.SalesOrders.Remove(order);
            }

            _ = await _db.SaveChangesAsync();
        }

        private async Task ApplyStockDeltaAsync(Guid productId, decimal quantityDelta)
        {
            if (quantityDelta == 0m)
            {
                return;
            }

            var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == productId);
            if (stock is null)
            {
                stock = new Stock
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Quantity = 0m,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _ = _db.Stocks.Add(stock);
            }

            stock.Quantity += quantityDelta;
            stock.UpdatedAt = DateTimeOffset.UtcNow;
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

        private static decimal GetSalesAdjustmentQuantity(ProductReturnItem item)
        {
            return Math.Max(item.Quantity, 0m);
        }

        private static decimal GetDamagedReturnQuantity(ProductReturnItem item)
        {
            return item.DamagedQuantity > 0 ? item.DamagedQuantity : item.IsDamaged ? item.Quantity : 0m;
        }

        private static bool IsInitialPayment(Payment payment)
        {
            return payment.Reference != null && payment.Reference.StartsWith("Initial payment for ");
        }
    }
}
