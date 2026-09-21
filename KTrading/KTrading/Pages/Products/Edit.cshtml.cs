using KTrading.Models;
using KTrading.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Products
{
    public class EditModel : PageModel
    {
        private readonly IProductService _service;
        private readonly ApplicationDbContext _db;

        public EditModel(IProductService service, ApplicationDbContext db)
        {
            _service = service;
            _db = db;
        }

        [BindProperty]
        public Product Product { get; set; } = new();

        [BindProperty]
        public List<ProductUnit> Units { get; set; } = new();

        public IEnumerable<SelectListItem> CategoryList { get; set; } = Array.Empty<SelectListItem>();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var entity = await _service.GetByIdAsync(id);
            if (entity is null) return NotFound();

            Product = entity;
            Units = await _db.ProductUnits.Where(u => u.ProductId == id && u.IsActive)
                .OrderBy(u => u.IsBaseUnit ? 0 : 1).ThenBy(u => u.Name).ToListAsync();
            CategoryList = await _db.ProductCategories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ValidateUnits();
            if (!ModelState.IsValid)
            {
                CategoryList = await _db.ProductCategories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
                return Page();
            }

            await _service.UpdateAsync(Product);
            var existing = await _db.ProductUnits.Where(u => u.ProductId == Product.Id).ToListAsync();
            foreach (var oldUnit in existing) oldUnit.IsActive = false;
            foreach (var posted in Units.Where(u => !string.IsNullOrWhiteSpace(u.Name)))
            {
                var unit = existing.FirstOrDefault(u => u.Id == posted.Id) ?? new ProductUnit { Id = Guid.NewGuid(), ProductId = Product.Id };
                if (!existing.Contains(unit)) _db.ProductUnits.Add(unit);
                unit.Name = posted.Name.Trim();
                unit.SKU = string.IsNullOrWhiteSpace(posted.SKU) ? null : posted.SKU.Trim();
                unit.ConversionFactor = posted.ConversionFactor;
                unit.SellingPrice = posted.SellingPrice;
                unit.PurchaseCost = posted.PurchaseCost;
                unit.IsBaseUnit = posted.IsBaseUnit;
                unit.IsActive = true;
            }
            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }

        private void ValidateUnits()
        {
            Units = Units.Where(u => !string.IsNullOrWhiteSpace(u.Name)).ToList();
            if (Units.Count == 0) return;
            if (Units.Count(u => u.IsBaseUnit) != 1)
                ModelState.AddModelError(nameof(Units), "Select exactly one base unit.");
            if (Units.Any(u => u.ConversionFactor <= 0))
                ModelState.AddModelError(nameof(Units), "Every conversion factor must be greater than zero.");
            var baseUnit = Units.FirstOrDefault(u => u.IsBaseUnit);
            if (baseUnit is not null) baseUnit.ConversionFactor = 1m;
            if (Units.GroupBy(u => u.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                ModelState.AddModelError(nameof(Units), "Unit names must be unique for a product.");
            if (baseUnit is not null)
            {
                Product.Unit = baseUnit.Name.Trim();
                Product.Price = baseUnit.SellingPrice;
                Product.Cost = baseUnit.PurchaseCost;
            }
        }
    }
}
