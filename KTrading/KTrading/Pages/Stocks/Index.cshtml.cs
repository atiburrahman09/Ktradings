using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using KTrading.Models;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Stocks
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public IEnumerable<Stock> Stocks { get; set; } = Array.Empty<Stock>();
        public Dictionary<Guid, string> ProductMap { get; set; } = new();

        public async Task OnGetAsync()
        {
            Stocks = await _db.Stocks.ToListAsync();
            var products = await _db.Products.ToListAsync();
            ProductMap = products.ToDictionary(p => p.Id, p => p.Name);
        }
    }
}
