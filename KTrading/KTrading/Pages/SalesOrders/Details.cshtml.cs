using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.SalesOrders
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public DetailsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public SalesOrder? Order { get; set; }
        public Customer? Customer { get; set; }
        public SalesOfficer? SalesOfficer { get; set; }
        public List<SalesOrderItem> Items { get; set; } = new();
        public List<SalesOrderItemDisplay> DisplayItems { get; set; } = new();
        public List<Payment> Payments { get; set; } = new();
        public Dictionary<Guid, string> ProductNames { get; set; } = new();
        public Dictionary<int, string> PaymentMethodNames { get; set; } = new();
        public IEnumerable<SelectListItem> PaymentMethodList { get; set; } = Array.Empty<SelectListItem>();
        public decimal ReturnedAmount { get; set; }
        public decimal AdjustedTotal => Math.Max((Order?.Total ?? 0m) - ReturnedAmount, 0m);
        public decimal AdjustedDue => Math.Max((Order?.DueAmount ?? 0m) - ReturnedAmount, 0m);
        public decimal AdjustedNet => AdjustedTotal - (Order?.Commission ?? 0m) - (Order?.Khajna ?? 0m) - (Order?.DsrSalary ?? 0m);

        [BindProperty]
        public decimal KhajnaAmount { get; set; }

        [BindProperty]
        public decimal DsrSalaryAmount { get; set; }

        [BindProperty]
        public decimal CollectionAmount { get; set; }

        [BindProperty]
        public int? PaymentMethodId { get; set; }

        [BindProperty]
        public string? CollectionReference { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var loaded = await LoadOrderAsync(id);
            if (!loaded) return NotFound();
            KhajnaAmount = Order!.Khajna;
            DsrSalaryAmount = Order.DsrSalary;
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateKhajnaAsync(Guid id)
        {
            var order = await _db.SalesOrders.FindAsync(id);
            if (order is null) return NotFound();

            if (KhajnaAmount < 0)
            {
                ModelState.AddModelError(nameof(KhajnaAmount), "Khajna cannot be negative.");
                await LoadOrderAsync(id);
                DsrSalaryAmount = Order!.DsrSalary;
                return Page();
            }

            order.Khajna = KhajnaAmount;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostUpdateDsrSalaryAsync(Guid id)
        {
            var order = await _db.SalesOrders.FindAsync(id);
            if (order is null) return NotFound();

            if (DsrSalaryAmount < 0)
            {
                ModelState.AddModelError(nameof(DsrSalaryAmount), "DSR Salary cannot be negative.");
                await LoadOrderAsync(id);
                KhajnaAmount = Order!.Khajna;
                return Page();
            }

            order.DsrSalary = DsrSalaryAmount;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostCollectDueAsync(Guid id)
        {
            var order = await _db.SalesOrders.FindAsync(id);
            if (order is null) return NotFound();

            if (CollectionAmount <= 0)
            {
                ModelState.AddModelError(nameof(CollectionAmount), "Collection amount must be greater than zero.");
            }

            var returnedAmount = await CalculateReturnedAmountAsync(id);
            var adjustedDue = Math.Max(order.DueAmount - returnedAmount, 0m);

            if (CollectionAmount > adjustedDue)
            {
                ModelState.AddModelError(nameof(CollectionAmount), "Collection amount cannot be greater than the due amount.");
            }

            if (!ModelState.IsValid)
            {
                await LoadOrderAsync(id);
                KhajnaAmount = Order!.Khajna;
                DsrSalaryAmount = Order.DsrSalary;
                return Page();
            }

            _db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                SalesOrderId = order.Id,
                PaymentMethodId = PaymentMethodId,
                PaymentDate = DateTimeOffset.UtcNow,
                Amount = CollectionAmount,
                Reference = string.IsNullOrWhiteSpace(CollectionReference)
                    ? $"Due collection for {order.OrderNumber}"
                    : CollectionReference.Trim()
            });

            order.PaidAmount += CollectionAmount;
            order.DueAmount = order.Total - order.PaidAmount;
            if (order.DueAmount < 0) order.DueAmount = 0;
            order.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            return RedirectToPage(new { id });
        }

        private async Task<bool> LoadOrderAsync(Guid id)
        {
            Order = await _db.SalesOrders.FindAsync(id);
            if (Order is null) return false;

            Customer = await _db.Customers.FindAsync(Order.CustomerId);
            if (Order.SalesOfficerId.HasValue)
            {
                SalesOfficer = await _db.SalesOfficers.FindAsync(Order.SalesOfficerId.Value);
            }
            Items = await _db.SalesOrderItems.Where(i => i.SalesOrderId == id).ToListAsync();
            var returnedQuantityByProduct = await GetReturnedQuantitiesAsync(id);
            DisplayItems = Items
                .GroupBy(i => i.ProductId)
                .Select(g =>
                {
                    var soldQuantity = g.Sum(i => i.Quantity);
                    var soldAmount = g.Sum(i => i.LineTotal);
                    var unitPrice = soldQuantity == 0 ? 0 : soldAmount / soldQuantity;
                    var returnedQuantity = returnedQuantityByProduct.GetValueOrDefault(g.Key);

                    return new SalesOrderItemDisplay
                    {
                        ProductId = g.Key,
                        Quantity = Math.Max(soldQuantity - returnedQuantity, 0m),
                        UnitPrice = unitPrice,
                        LineTotal = Math.Max(soldAmount - (returnedQuantity * unitPrice), 0m)
                    };
                })
                .OrderBy(i => i.ProductId)
                .ToList();
            Payments = await _db.Payments.Where(p => p.SalesOrderId == id).OrderBy(p => p.PaymentDate).ToListAsync();
            PaymentMethodNames = await _db.PaymentMethods.ToDictionaryAsync(m => m.Id, m => m.Name);
            PaymentMethodList = PaymentMethodNames.Select(m => new SelectListItem(m.Value, m.Key.ToString())).ToList();

            var products = await _db.Products.ToListAsync();
            var itemProductIds = Items.Select(i => i.ProductId).ToHashSet();
            ProductNames = products
                .Where(p => itemProductIds.Contains(p.Id))
                .ToDictionary(p => p.Id, p => p.Name);
            ReturnedAmount = await CalculateReturnedAmountAsync(id);

            return true;
        }

        private async Task<decimal> CalculateReturnedAmountAsync(Guid salesOrderId)
        {
            var salesItems = await _db.SalesOrderItems
                .Where(i => i.SalesOrderId == salesOrderId)
                .ToListAsync();
            var unitPrices = salesItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(i => i.Quantity) == 0 ? 0 : g.Sum(i => i.LineTotal) / g.Sum(i => i.Quantity));
            var returnItems = await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == salesOrderId),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => item)
                .ToListAsync();

            return returnItems.Sum(i => i.Quantity * unitPrices.GetValueOrDefault(i.ProductId));
        }

        private async Task<Dictionary<Guid, decimal>> GetReturnedQuantitiesAsync(Guid salesOrderId)
        {
            return await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == salesOrderId),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => item)
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);
        }

        public class SalesOrderItemDisplay
        {
            public Guid ProductId { get; set; }
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal LineTotal { get; set; }
        }
    }
}
