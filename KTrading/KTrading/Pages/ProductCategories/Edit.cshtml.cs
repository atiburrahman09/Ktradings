using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KTrading.Pages.ProductCategories
{
    [Authorize(Policy = "RequireAdminOrSales")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public EditModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public ProductCategory ProductCategory { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var entity = await _db.ProductCategories.FindAsync(id);
            if (entity is null) return NotFound();

            ProductCategory = entity;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            _db.ProductCategories.Update(ProductCategory);
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}

