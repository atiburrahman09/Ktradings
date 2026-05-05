using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.ProductReturns
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public IEnumerable<ProductReturn> Returns { get; set; } = Array.Empty<ProductReturn>();
        public Dictionary<Guid, string> CustomerMap { get; set; } = new();
        public PaginationModel Pager { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public async Task OnGetAsync()
        {
            const int pageSize = 10;
            PageNumber = Math.Max(PageNumber, 1);

            var query = _db.ProductReturns
                .Include(r => r.Items)
                .OrderByDescending(r => r.CreatedAt);
            var totalItems = await query.CountAsync();
            Pager = new PaginationModel { PageNumber = PageNumber, PageSize = pageSize, TotalItems = totalItems };
            Returns = await query
                .Skip((PageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var customers = await _db.Customers.ToListAsync();
            CustomerMap = customers.ToDictionary(c => c.Id, c => c.Name);
        }
    }
}
