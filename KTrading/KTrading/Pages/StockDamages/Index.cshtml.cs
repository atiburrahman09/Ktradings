using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.StockDamages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<StockMovement> Damages { get; set; } = new();
        public Dictionary<Guid, string> ProductNames { get; set; } = new();
        public Dictionary<Guid, string?> ProductSkus { get; set; } = new();
        public PaginationModel Pager { get; set; } = new();

        public string? SearchTerm { get; set; }
        public int PageNumber { get; set; } = 1;

        public async Task OnGetAsync(string? searchTerm, int pageNumber = 1)
        {
            const int pageSize = 10;
            SearchTerm = searchTerm;
            PageNumber = Math.Max(pageNumber, 1);

            var query = _db.StockMovements
                .Where(m => m.MovementType == "DAMAGE" && m.Quantity < 0);

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var search = SearchTerm.Trim();
                var productIds = await _db.Products
                    .Where(p => p.Name.Contains(search) || (p.SKU != null && p.SKU.Contains(search)))
                    .Select(p => p.Id)
                    .ToListAsync();
                query = query.Where(m => productIds.Contains(m.ProductId) || (m.Note != null && m.Note.Contains(search)));
            }

            var totalItems = await query.CountAsync();
            Pager = new PaginationModel { PageNumber = PageNumber, PageSize = pageSize, TotalItems = totalItems };
            Damages = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((PageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var productIdsForPage = Damages.Select(d => d.ProductId).Distinct().ToList();
            var products = await _db.Products
                .Where(p => productIdsForPage.Contains(p.Id))
                .ToListAsync();
            ProductNames = products.ToDictionary(p => p.Id, p => p.Name);
            ProductSkus = products.ToDictionary(p => p.Id, p => p.SKU);
        }
    }
}
