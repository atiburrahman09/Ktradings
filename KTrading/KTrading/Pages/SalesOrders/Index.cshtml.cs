using KTrading.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.SalesOrders
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public IEnumerable<SalesOrder> Orders { get; set; } = Array.Empty<SalesOrder>();

        public async Task OnGetAsync()
        {
            Orders = await _db.SalesOrders.ToListAsync();
        }
    }
}
