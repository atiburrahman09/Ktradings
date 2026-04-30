using KTrading.Data;
using KTrading.Models;
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

        public async Task OnGetAsync()
        {
            Customers = await _db.Customers.ToListAsync();
        }
    }
}
