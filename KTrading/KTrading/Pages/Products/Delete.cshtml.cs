using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Products
{
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public DeleteModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public Product? Product { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            Product = await _db.Products
                .Include(p => p.ProductCategory)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (Product is null) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            var entity = await _db.Products.FindAsync(id);
            if (entity is null) return NotFound();

            _db.Products.Remove(entity);
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}
