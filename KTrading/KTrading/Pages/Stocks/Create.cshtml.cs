using System.ComponentModel.DataAnnotations;
using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Stocks
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
        public StockInInput Input { get; set; } = new();

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

            var productExists = await _db.Products.AnyAsync(p => p.Id == Input.ProductId && p.IsActive);
            if (!productExists)
            {
                ModelState.AddModelError(nameof(Input.ProductId), "Please select an active product.");
                await LoadProductsAsync();
                return Page();
            }

            var now = DateTimeOffset.UtcNow;
            var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == Input.ProductId);

            if (stock is null)
            {
                stock = new Stock
                {
                    Id = Guid.NewGuid(),
                    ProductId = Input.ProductId,
                    Quantity = Input.Quantity,
                    UpdatedAt = now
                };
                _db.Stocks.Add(stock);
            }
            else
            {
                stock.Quantity += Input.Quantity;
                stock.UpdatedAt = now;
            }

            _db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = Input.ProductId,
                Quantity = Input.Quantity,
                MovementType = "IN",
                ReferenceId = stock.Id,
                Note = string.IsNullOrWhiteSpace(Input.Note) ? "Product in" : Input.Note.Trim(),
                CreatedAt = now
            });

            await _db.SaveChangesAsync();
            return RedirectToPage("Details", new { id = stock.Id });
        }

        private async Task LoadProductsAsync()
        {
            ProductList = await _db.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new SelectListItem(
                    string.IsNullOrWhiteSpace(p.SKU) ? p.Name : $"{p.Name} ({p.SKU})",
                    p.Id.ToString()))
                .ToListAsync();
        }

        public class StockInInput
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
