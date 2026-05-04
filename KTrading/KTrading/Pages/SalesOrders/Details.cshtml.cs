using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
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
        public List<SalesOrderItem> Items { get; set; } = new();
        public List<Payment> Payments { get; set; } = new();
        public Dictionary<Guid, string> ProductNames { get; set; } = new();

        [BindProperty]
        public decimal KhajnaAmount { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var loaded = await LoadOrderAsync(id);
            if (!loaded) return NotFound();
            KhajnaAmount = Order!.Khajna;
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
                return Page();
            }

            order.Khajna = KhajnaAmount;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        private async Task<bool> LoadOrderAsync(Guid id)
        {
            Order = await _db.SalesOrders.FindAsync(id);
            if (Order is null) return false;

            Customer = await _db.Customers.FindAsync(Order.CustomerId);
            Items = await _db.SalesOrderItems.Where(i => i.SalesOrderId == id).ToListAsync();
            Payments = await _db.Payments.Where(p => p.SalesOrderId == id).OrderBy(p => p.PaymentDate).ToListAsync();

            var products = await _db.Products.ToListAsync();
            var itemProductIds = Items.Select(i => i.ProductId).ToHashSet();
            ProductNames = products
                .Where(p => itemProductIds.Contains(p.Id))
                .ToDictionary(p => p.Id, p => p.Name);

            return true;
        }
    }
}
