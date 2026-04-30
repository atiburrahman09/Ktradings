using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KTrading.Pages.ProductCategories
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
        public ProductCategory ProductCategory { get; set; } = new();

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            if (ProductCategory.Id == Guid.Empty) ProductCategory.Id = Guid.NewGuid();

            _db.ProductCategories.Add(ProductCategory);
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}

