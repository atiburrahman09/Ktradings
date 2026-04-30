using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.ProductCategories
{
    [Authorize(Policy = "RequireAdminOrSales")]
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public DeleteModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public ProductCategory? ProductCategory { get; set; }

        public int LinkedProductCount { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            ProductCategory = await _db.ProductCategories.FindAsync(id);
            if (ProductCategory is null) return NotFound();

            LinkedProductCount = await _db.Products.CountAsync(p => p.ProductCategoryId == id);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            var entity = await _db.ProductCategories.FindAsync(id);
            if (entity is null) return NotFound();

            _db.ProductCategories.Remove(entity);
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}

