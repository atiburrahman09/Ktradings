using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.SalesOrders
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public DetailsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public SalesOrder? Order { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            Order = await _db.SalesOrders.FindAsync(id);
            if (Order is null) return NotFound();
            return Page();
        }
    }
}
