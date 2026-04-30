using KTrading.Models;
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

        public async Task OnGetAsync()
        {
            Returns = await _db.ProductReturns.Include(r => r.Items).ToListAsync();
            var customers = await _db.Customers.ToListAsync();
            CustomerMap = customers.ToDictionary(c => c.Id, c => c.Name);
        }
    }
}
