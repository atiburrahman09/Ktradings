using KTrading.Models;
using KTrading.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using KTrading.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Products
{
    [Authorize(Policy = "RequireAdminOrSales")]
    public class CreateModel : PageModel
    {
        private readonly IProductService _service;
        private readonly ApplicationDbContext _db;

        public CreateModel(IProductService service, ApplicationDbContext db)
        {
            _service = service;
            _db = db;
        }

        [BindProperty]
        public Product Product { get; set; } = new();

        [BindProperty]
        public List<ProductUnit> Units { get; set; } = new();

        public IEnumerable<SelectListItem> CategoryList { get; set; } = Array.Empty<SelectListItem>();

        public async Task OnGetAsync()
        {
            Units = new List<ProductUnit> { new() { IsBaseUnit = true, ConversionFactor = 1m, IsActive = true } };
            CategoryList = await _db.ProductCategories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ValidateUnits();
            if (!ModelState.IsValid)
            {
                CategoryList = await _db.ProductCategories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
                return Page();
            }

            await _service.AddAsync(Product);
            foreach (var unit in Units.Where(u => !string.IsNullOrWhiteSpace(u.Name)))
            {
                unit.Id = Guid.NewGuid();
                unit.ProductId = Product.Id;
                unit.Name = unit.Name.Trim();
                unit.IsActive = true;
                _db.ProductUnits.Add(unit);
            }
            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }

        private void ValidateUnits()
        {
            Units = Units.Where(u => !string.IsNullOrWhiteSpace(u.Name)).ToList();
            if (Units.Count == 0) return; // Empty means a legacy single-unit product.
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
