using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Customers
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public IEnumerable<Customer> Customers { get; set; } = Array.Empty<Customer>();
        public PaginationModel Pager { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public async Task OnGetAsync()
        {
            const int pageSize = 10;
            PageNumber = Math.Max(PageNumber, 1);

            var query = _db.Customers.OrderBy(c => c.Name);
            var totalItems = await query.CountAsync();
            Pager = new PaginationModel { PageNumber = PageNumber, PageSize = pageSize, TotalItems = totalItems };
            Customers = await query
                .Skip((PageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}
