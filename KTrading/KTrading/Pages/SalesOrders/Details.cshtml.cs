using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using KTrading.Services;
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
        public List<PaymentDisplay> DisplayPayments { get; set; } = new();
        public Dictionary<Guid, string> ProductNames { get; set; } = new();
        public Dictionary<int, string> PaymentMethodNames { get; set; } = new();
        public IEnumerable<SelectListItem> PaymentMethodList { get; set; } = Array.Empty<SelectListItem>();
        public decimal ReturnedAmount { get; set; }
        public decimal DamageAmount { get; set; }
        public decimal AdjustedTotal => Math.Max((Order?.Total ?? 0m) - ReturnedAmount, 0m);
        public decimal AdjustedDue => Math.Max(Order?.DueAmount ?? 0m, 0m);
        public decimal AdjustedNet => SalesOrderFinancials.CalculateNetAmount(
            AdjustedTotal,
            Order?.Commission ?? 0m,
            Order?.DsrSalary ?? 0m,
            DamageAmount,
            Order?.OtherCosting ?? 0m);
        public decimal DisplayPaidAmount => SalesOrderFinancials.CalculatePaidAmount(
            AdjustedTotal,
            Order?.Commission ?? 0m,
            Order?.DsrSalary ?? 0m,
            DamageAmount,
            Order?.OtherCosting ?? 0m,
            AdjustedDue);

        [BindProperty]
        public decimal KhajnaAmount { get; set; }

        [BindProperty]
        public decimal DsrSalaryAmount { get; set; }

        [BindProperty]
        public decimal OtherCostingAmount { get; set; }

        [BindProperty]
        public string? OtherCostingNote { get; set; }

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
            OtherCostingAmount = Order.OtherCosting;
            OtherCostingNote = Order.OtherCostingNote;
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
                OtherCostingAmount = Order.OtherCosting;
                OtherCostingNote = Order.OtherCostingNote;
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
                OtherCostingAmount = Order.OtherCosting;
                OtherCostingNote = Order.OtherCostingNote;
                return Page();
            }

            order.DsrSalary = DsrSalaryAmount;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await RecalculatePaidAndInitialPaymentAsync(order);
            await _db.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostUpdateOtherCostingAsync(Guid id)
        {
            var order = await _db.SalesOrders.FindAsync(id);
            if (order is null) return NotFound();

            if (OtherCostingAmount < 0)
            {
                ModelState.AddModelError(nameof(OtherCostingAmount), "Other costing cannot be negative.");
            }

            if (!string.IsNullOrWhiteSpace(OtherCostingNote) && OtherCostingNote.Length > 1000)
            {
                ModelState.AddModelError(nameof(OtherCostingNote), "Other costing note cannot be longer than 1000 characters.");
            }

            if (!ModelState.IsValid)
            {
                await LoadOrderAsync(id);
                KhajnaAmount = Order!.Khajna;
                DsrSalaryAmount = Order.DsrSalary;
                return Page();
            }

            order.OtherCosting = OtherCostingAmount;
            order.OtherCostingNote = string.IsNullOrWhiteSpace(OtherCostingNote) ? null : OtherCostingNote.Trim();
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await RecalculatePaidAndInitialPaymentAsync(order);
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
            var adjustedDue = Math.Max(order.DueAmount, 0m);

            if (CollectionAmount > adjustedDue)
            {
                ModelState.AddModelError(nameof(CollectionAmount), "Collection amount cannot be greater than the due amount.");
            }

            if (!ModelState.IsValid)
            {
                await LoadOrderAsync(id);
                KhajnaAmount = Order!.Khajna;
                DsrSalaryAmount = Order.DsrSalary;
                OtherCostingAmount = Order.OtherCosting;
                OtherCostingNote = Order.OtherCostingNote;
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
            order.DueAmount = Math.Max(order.DueAmount - CollectionAmount, 0m);
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
            var salesAdjustmentQuantityByProduct = await GetSalesAdjustmentQuantitiesAsync(id);
            DisplayItems = Items
                .GroupBy(i => i.ProductId)
                .Select(g =>
                {
                    var soldQuantity = g.Sum(i => i.Quantity);
                    var soldAmount = g.Sum(i => i.LineTotal);
                    var unitPrice = soldQuantity == 0 ? 0 : soldAmount / soldQuantity;
                    var salesAdjustmentQuantity = salesAdjustmentQuantityByProduct.GetValueOrDefault(g.Key);

                    return new SalesOrderItemDisplay
                    {
                        ProductId = g.Key,
                        Quantity = Math.Max(soldQuantity - salesAdjustmentQuantity, 0m),
                        UnitPrice = unitPrice,
                        LineTotal = Math.Max(soldAmount - (salesAdjustmentQuantity * unitPrice), 0m)
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
            DamageAmount = await CalculateDamageAmountAsync(id);
            DisplayPayments = BuildDisplayPayments(Payments, DisplayPaidAmount);

            return true;
        }

        private async Task RecalculatePaidAndInitialPaymentAsync(SalesOrder order)
        {
            var returnedAmount = await CalculateReturnedAmountAsync(order.Id);
            var damageAmount = await CalculateDamageAmountAsync(order.Id);
            var adjustedTotal = Math.Max(order.Total - returnedAmount, 0m);
            order.PaidAmount = SalesOrderFinancials.CalculatePaidAmount(
                adjustedTotal,
                order.Commission,
                order.DsrSalary,
                damageAmount,
                order.OtherCosting,
                Math.Max(order.DueAmount, 0m));

            await ReconcileInitialPaymentAsync(order);
        }

        private async Task ReconcileInitialPaymentAsync(SalesOrder order)
        {
            var payments = await _db.Payments
                .Where(p => p.SalesOrderId == order.Id)
                .ToListAsync();
            var initialPayment = payments.FirstOrDefault(IsInitialPayment);
            var otherPaymentsTotal = payments
                .Where(p => p.Id != initialPayment?.Id)
                .Sum(p => p.Amount);
            var initialAmount = Math.Max(order.PaidAmount - otherPaymentsTotal, 0m);

            if (initialAmount == 0m)
            {
                if (initialPayment is not null)
                {
                    _db.Payments.Remove(initialPayment);
                }

                return;
            }

            if (initialPayment is null)
            {
                _db.Payments.Add(new Payment
                {
                    Id = Guid.NewGuid(),
                    SalesOrderId = order.Id,
                    PaymentDate = order.OrderDate,
                    Amount = initialAmount,
                    Reference = $"Initial payment for {order.OrderNumber}"
                });
                return;
            }

            initialPayment.PaymentDate = order.OrderDate;
            initialPayment.Amount = initialAmount;
            initialPayment.Reference = $"Initial payment for {order.OrderNumber}";
        }

        private static List<PaymentDisplay> BuildDisplayPayments(IEnumerable<Payment> payments, decimal paidAmount)
        {
            var paymentList = payments.OrderBy(p => p.PaymentDate).ToList();
            var initialPayment = paymentList.FirstOrDefault(IsInitialPayment);
            var otherPaymentsTotal = paymentList
                .Where(p => p.Id != initialPayment?.Id)
                .Sum(p => p.Amount);
            var adjustedInitialAmount = Math.Max(paidAmount - otherPaymentsTotal, 0m);

            return paymentList
                .Where(p => !IsInitialPayment(p) || adjustedInitialAmount > 0m)
                .Select(p => new PaymentDisplay
                {
                    Payment = p,
                    Amount = IsInitialPayment(p) ? adjustedInitialAmount : p.Amount
                })
                .ToList();
        }

        private static bool IsInitialPayment(Payment payment)
        {
            return payment.Reference != null && payment.Reference.StartsWith("Initial payment for ");
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
                .Where(i => !i.IsOutsideSalesDamageReturn)
                .ToListAsync();

            return returnItems.Sum(i => GetSalesAdjustmentQuantity(i) * unitPrices.GetValueOrDefault(i.ProductId));
        }

        private async Task<Dictionary<Guid, decimal>> GetReturnedQuantitiesAsync(Guid salesOrderId)
        {
            var returnItems = await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == salesOrderId),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => item)
                .Where(i => !i.IsOutsideSalesDamageReturn)
                .ToListAsync();

            return returnItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        }

        private async Task<Dictionary<Guid, decimal>> GetSalesAdjustmentQuantitiesAsync(Guid salesOrderId)
        {
            var returnItems = await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == salesOrderId),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => item)
                .Where(i => !i.IsOutsideSalesDamageReturn)
                .ToListAsync();

            return returnItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(GetSalesAdjustmentQuantity));
        }

        private static decimal GetSalesAdjustmentQuantity(ProductReturnItem item)
        {
            return Math.Max(item.Quantity, 0m);
        }

        private async Task<decimal> CalculateDamageAmountAsync(Guid salesOrderId)
        {
            var order = await _db.SalesOrders.FindAsync(salesOrderId);
            if (order is null)
            {
                return 0m;
            }

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
                .Where(i => !i.IsOutsideSalesDamageReturn)
                .ToListAsync();

            var orderDamageAmount = returnItems.Sum(i => GetDamagedReturnQuantity(i) * unitPrices.GetValueOrDefault(i.ProductId));
            var outsideDamageAmount = await CalculateOutsideSalesDamageAmountAsync(order);

            return orderDamageAmount + outsideDamageAmount;
        }

        private async Task<decimal> CalculateOutsideSalesDamageAmountAsync(SalesOrder order)
        {
            var outsideDamageItems = await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == order.Id),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => item)
                .Where(i => i.IsOutsideSalesDamageReturn)
                .ToListAsync();
            var outsideDamageProductIds = outsideDamageItems.Select(i => i.ProductId).Distinct().ToList();
            var outsideDamagePrices = outsideDamageProductIds.Any()
                ? await _db.Products
                    .Where(p => outsideDamageProductIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Price)
                : new Dictionary<Guid, decimal>();

            return outsideDamageItems.Sum(i => GetDamagedReturnQuantity(i) * outsideDamagePrices.GetValueOrDefault(i.ProductId));
        }

        private static decimal GetDamagedReturnQuantity(ProductReturnItem item)
        {
            return item.DamagedQuantity > 0 ? item.DamagedQuantity : item.IsDamaged ? item.Quantity : 0m;
        }

        public class SalesOrderItemDisplay
        {
            public Guid ProductId { get; set; }
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal LineTotal { get; set; }
        }

        public class PaymentDisplay
        {
            public Payment Payment { get; set; } = new();
            public decimal Amount { get; set; }
        }
    }
}
