using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.ProductCategories
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public IEnumerable<ProductCategory> ProductCategories { get; set; } = Array.Empty<ProductCategory>();

        public async Task OnGetAsync()
        {
            ProductCategories = await _db.ProductCategories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
    }
}

