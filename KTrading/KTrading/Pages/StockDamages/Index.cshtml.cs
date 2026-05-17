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

        public List<DamageListItem> Damages { get; set; } = new();
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

            var damageMovements = await _db.StockMovements
                .Where(m => m.MovementType == "DAMAGE" && m.Quantity < 0)
                .Select(m => new DamageListItem
                {
                    CreatedAt = m.CreatedAt,
                    ProductId = m.ProductId,
                    Quantity = Math.Abs(m.Quantity),
                    Note = m.Note
                })
                .ToListAsync();

            var movementReturnDamageKeys = await _db.StockMovements
                .Where(m => m.MovementType == "DAMAGE" && m.Quantity < 0 && m.ReferenceId.HasValue)
                .Select(m => new { ReturnId = m.ReferenceId!.Value, m.ProductId })
                .ToListAsync();
            var movementReturnDamageKeySet = movementReturnDamageKeys
                .Select(k => (k.ReturnId, k.ProductId))
                .ToHashSet();

            var damagedReturnItems = await _db.ProductReturnItems
                .Join(_db.ProductReturns,
                    item => item.ProductReturnId,
                    productReturn => productReturn.Id,
                    (item, productReturn) => new
                    {
                        productReturn.Id,
                        productReturn.ReturnNumber,
                        productReturn.CreatedAt,
                        item.ProductId,
                        item.Quantity,
                        item.DamagedQuantity,
                        item.IsDamaged,
                        item.Notes
                    })
                .Where(i => i.DamagedQuantity > 0 || i.IsDamaged)
                .ToListAsync();

            var returnDamages = damagedReturnItems
                .Where(i => !movementReturnDamageKeySet.Contains((i.Id, i.ProductId)))
                .Select(i => new DamageListItem
                {
                    CreatedAt = i.CreatedAt,
                    ProductId = i.ProductId,
                    Quantity = i.DamagedQuantity > 0 ? i.DamagedQuantity : i.Quantity,
                    Note = string.IsNullOrWhiteSpace(i.Notes)
                        ? $"Damaged return {i.ReturnNumber}"
                        : $"{i.Notes} ({i.ReturnNumber})"
                })
                .Where(i => i.Quantity > 0)
                .ToList();

            var allDamages = damageMovements.Concat(returnDamages);

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var search = SearchTerm.Trim();
                var productIds = await _db.Products
                    .Where(p => p.Name.Contains(search) || (p.SKU != null && p.SKU.Contains(search)))
                    .Select(p => p.Id)
                    .ToListAsync();
                allDamages = allDamages
                    .Where(m => productIds.Contains(m.ProductId) ||
                        (m.Note != null && m.Note.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            var orderedDamages = allDamages
                .OrderByDescending(m => m.CreatedAt)
                .ToList();
            var totalItems = orderedDamages.Count;
            Pager = new PaginationModel { PageNumber = PageNumber, PageSize = pageSize, TotalItems = totalItems };
            Damages = orderedDamages
                .Skip((PageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var productIdsForPage = Damages.Select(d => d.ProductId).Distinct().ToList();
            var products = await _db.Products
                .Where(p => productIdsForPage.Contains(p.Id))
                .ToListAsync();
            ProductNames = products.ToDictionary(p => p.Id, p => p.Name);
            ProductSkus = products.ToDictionary(p => p.Id, p => p.SKU);
        }

        public class DamageListItem
        {
            public DateTimeOffset CreatedAt { get; set; }
            public Guid ProductId { get; set; }
            public decimal Quantity { get; set; }
            public string? Note { get; set; }
        }
    }
}
