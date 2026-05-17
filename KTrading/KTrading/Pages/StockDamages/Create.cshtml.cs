using System.ComponentModel.DataAnnotations;
using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.StockDamages
{
    [Authorize(Policy = "RequireAdminOrSales")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public CreateModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public DamageInput Input { get; set; } = new();

        public IEnumerable<SelectListItem> ProductList { get; set; } = Array.Empty<SelectListItem>();

        public async Task OnGetAsync()
        {
            await LoadProductsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadProductsAsync();
                return Page();
            }

            var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == Input.ProductId);
            var available = stock?.Quantity ?? 0m;
            if (Input.Quantity > available)
            {
                ModelState.AddModelError(nameof(Input.Quantity), $"Only {available:N2} is available in stock.");
                await LoadProductsAsync();
                return Page();
            }

            var now = DateTimeOffset.UtcNow;
            if (stock is null)
            {
                stock = new Stock { Id = Guid.NewGuid(), ProductId = Input.ProductId, Quantity = 0, UpdatedAt = now };
                _db.Stocks.Add(stock);
            }

            stock.Quantity -= Input.Quantity;
            stock.UpdatedAt = now;

            _db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = Input.ProductId,
                Quantity = -Input.Quantity,
                MovementType = "DAMAGE",
                ReferenceId = stock.Id,
                Note = string.IsNullOrWhiteSpace(Input.Note) ? "Stock damage" : Input.Note.Trim(),
                CreatedAt = now
            });

            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }

        private async Task LoadProductsAsync()
        {
            var products = await _db.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();
            var stockByProduct = await _db.Stocks.ToDictionaryAsync(s => s.ProductId, s => s.Quantity);

            ProductList = products.Select(p =>
            {
                var stock = stockByProduct.GetValueOrDefault(p.Id);
                var label = string.IsNullOrWhiteSpace(p.SKU) ? p.Name : $"{p.Name} ({p.SKU})";
                return new SelectListItem($"{label} - Stock: {stock:N2}", p.Id.ToString());
            }).ToList();
        }

        public class DamageInput
        {
            [Required]
            public Guid ProductId { get; set; }

            [Range(typeof(decimal), "0.0001", "999999999", ErrorMessage = "Quantity must be greater than zero.")]
            public decimal Quantity { get; set; }

            [MaxLength(1000)]
            public string? Note { get; set; }
        }
    }
}
