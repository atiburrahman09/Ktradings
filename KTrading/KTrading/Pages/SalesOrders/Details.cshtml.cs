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
        public List<Payment> Payments { get; set; } = new();
        public Dictionary<Guid, string> ProductNames { get; set; } = new();
        public Dictionary<int, string> PaymentMethodNames { get; set; } = new();
        public IEnumerable<SelectListItem> PaymentMethodList { get; set; } = Array.Empty<SelectListItem>();

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

        [BindProperty]
        public Guid DamageProductId { get; set; }

        [BindProperty]
        public decimal DamageQuantity { get; set; }

        [BindProperty]
        public string? DamageNote { get; set; }

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

            if (CollectionAmount > order.DueAmount)
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

        public async Task<IActionResult> OnPostRecordDamageAsync(Guid id)
        {
            var order = await _db.SalesOrders.FindAsync(id);
            if (order is null) return NotFound();

            var itemExists = await _db.SalesOrderItems.AnyAsync(i => i.SalesOrderId == id && i.ProductId == DamageProductId);
            if (!itemExists)
            {
                ModelState.AddModelError(nameof(DamageProductId), "Select a product from this order.");
            }

            if (DamageQuantity <= 0)
            {
                ModelState.AddModelError(nameof(DamageQuantity), "Damage quantity must be greater than zero.");
            }

            if (!ModelState.IsValid)
            {
                await LoadOrderAsync(id);
                KhajnaAmount = Order!.Khajna;
                DsrSalaryAmount = Order.DsrSalary;
                return Page();
            }

            _db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = DamageProductId,
                Quantity = -DamageQuantity,
                MovementType = "DAMAGE",
                ReferenceId = order.Id,
                Note = string.IsNullOrWhiteSpace(DamageNote)
                    ? $"Damage against {order.OrderNumber}"
                    : DamageNote.Trim(),
                CreatedAt = DateTimeOffset.UtcNow
            });

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
            Payments = await _db.Payments.Where(p => p.SalesOrderId == id).OrderBy(p => p.PaymentDate).ToListAsync();
            PaymentMethodNames = await _db.PaymentMethods.ToDictionaryAsync(m => m.Id, m => m.Name);
            PaymentMethodList = PaymentMethodNames.Select(m => new SelectListItem(m.Value, m.Key.ToString())).ToList();

            var products = await _db.Products.ToListAsync();
            var itemProductIds = Items.Select(i => i.ProductId).ToHashSet();
            ProductNames = products
                .Where(p => itemProductIds.Contains(p.Id))
                .ToDictionary(p => p.Id, p => p.Name);

            return true;
        }
    }
}
