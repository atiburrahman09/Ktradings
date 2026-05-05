using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Products
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public IEnumerable<Product> Products { get; set; } = Array.Empty<Product>();
        public PaginationModel Pager { get; set; } = new();

        public IEnumerable<SelectListItem> CategoryList { get; set; } = Array.Empty<SelectListItem>();

        [BindProperty(SupportsGet = true)]
        public Guid? CategoryId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public async Task OnGetAsync()
        {
            const int pageSize = 10;
            PageNumber = Math.Max(PageNumber, 1);

            CategoryList = await _db.ProductCategories
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
                .ToListAsync();

            var query = _db.Products
                .Include(p => p.ProductCategory)
                .AsQueryable();

            if (CategoryId.HasValue)
            {
                query = query.Where(p => p.ProductCategoryId == CategoryId.Value);
            }

            var totalItems = await query.CountAsync();
            Pager = new PaginationModel { PageNumber = PageNumber, PageSize = pageSize, TotalItems = totalItems };
            Products = await query
                .OrderBy(p => p.Name)
                .Skip((PageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}
