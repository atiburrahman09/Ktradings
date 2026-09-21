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
        public List<ProductUnit> ProductUnits { get; set; } = new();

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

            ProductUnit? selectedUnit = null;
            if (Input.ProductUnitId.HasValue)
            {
                selectedUnit = await _db.ProductUnits.FirstOrDefaultAsync(u => u.Id == Input.ProductUnitId && u.ProductId == Input.ProductId && u.IsActive);
                if (selectedUnit is null)
                {
                    ModelState.AddModelError(nameof(Input.ProductUnitId), "Select a valid unit for this product.");
                    await LoadProductsAsync();
                    return Page();
                }
            }
            var factor = selectedUnit?.ConversionFactor ?? 1m;
            var baseQuantity = Input.Quantity * factor;
            var now = DateTimeOffset.UtcNow;
            var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == Input.ProductId);

            if (stock is null)
            {
                stock = new Stock
                {
                    Id = Guid.NewGuid(),
                    ProductId = Input.ProductId,
                    Quantity = baseQuantity,
                    UpdatedAt = now
                };
                _db.Stocks.Add(stock);
            }
            else
            {
                stock.Quantity += baseQuantity;
                stock.UpdatedAt = now;
            }

            _db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = Input.ProductId,
                Quantity = baseQuantity,
                EnteredQuantity = Input.Quantity,
                ProductUnitId = selectedUnit?.Id,
                UnitName = selectedUnit?.Name,
                ConversionFactor = factor,
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
            ProductUnits = await _db.ProductUnits.Where(u => u.IsActive)
                .OrderBy(u => u.IsBaseUnit ? 0 : 1).ThenBy(u => u.Name).ToListAsync();
        }

        public class StockInInput
        {
            [Required]
            public Guid ProductId { get; set; }

            [Range(typeof(decimal), "0.0001", "999999999", ErrorMessage = "Quantity must be greater than zero.")]
            public decimal Quantity { get; set; }

            public Guid? ProductUnitId { get; set; }

            [MaxLength(1000)]
            public string? Note { get; set; }
        }
    }
}
