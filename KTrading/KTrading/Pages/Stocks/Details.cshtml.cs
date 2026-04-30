using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using KTrading.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace KTrading.Pages.Stocks
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public DetailsModel(ApplicationDbContext db) { _db = db; }

        public Stock Stock { get; set; } = new();
        public string ProductName { get; set; } = "";
        public IEnumerable<StockMovement> Movements { get; set; } = Array.Empty<StockMovement>();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            Stock = await _db.Stocks.FindAsync(id);
            if (Stock == null) return NotFound();
            var p = await _db.Products.FindAsync(Stock.ProductId);
            ProductName = p?.Name ?? Stock.ProductId.ToString();
            Movements = await _db.StockMovements.Where(s => s.ProductId == Stock.ProductId).OrderByDescending(s => s.CreatedAt).Take(50).ToListAsync();
            return Page();
        }
    }
}
