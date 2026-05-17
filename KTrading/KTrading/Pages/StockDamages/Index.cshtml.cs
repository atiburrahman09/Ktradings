using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.StockDamages
{
    [Authorize(Policy = "RequireAdminOrSales")]
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

            var damageMovementRows = await _db.StockMovements
                .Where(m => m.MovementType == "DAMAGE" && m.Quantity < 0)
                .ToListAsync();
            var damageMovements = damageMovementRows
                .Select(m => new DamageListItem
                {
                    Id = m.Id,
                    CreatedAt = m.CreatedAt,
                    ProductId = m.ProductId,
                    Quantity = Math.Abs(m.Quantity),
                    Note = m.Note,
                    Source = DamageSource.Movement
                })
                .ToList();

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
                    Id = i.Id,
                    CreatedAt = i.CreatedAt,
                    ProductId = i.ProductId,
                    Quantity = i.DamagedQuantity > 0 ? i.DamagedQuantity : i.Quantity,
                    Note = string.IsNullOrWhiteSpace(i.Notes)
                        ? $"Damaged return {i.ReturnNumber}"
                        : $"{i.Notes} ({i.ReturnNumber})",
                    Source = DamageSource.ReturnItem
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

        public async Task<IActionResult> OnPostDeleteAsync(Guid id, string? source, string? searchTerm, int pageNumber = 1)
        {
            if (string.Equals(source, DamageSource.ReturnItem, StringComparison.OrdinalIgnoreCase))
            {
                var item = await _db.ProductReturnItems.FindAsync(id);
                if (item is null)
                {
                    return NotFound();
                }

                if (item.Quantity <= 0m)
                {
                    _db.ProductReturnItems.Remove(item);
                }
                else
                {
                    item.DamagedQuantity = 0m;
                    item.IsDamaged = false;
                }

                await _db.SaveChangesAsync();
                return RedirectToPage(new { searchTerm, pageNumber });
            }

            var movement = await _db.StockMovements
                .FirstOrDefaultAsync(m => m.Id == id && m.MovementType == "DAMAGE" && m.Quantity < 0);
            if (movement is null)
            {
                return NotFound();
            }

            _db.StockMovements.Remove(movement);

            await _db.SaveChangesAsync();

            return RedirectToPage(new { searchTerm, pageNumber });
        }

        public class DamageListItem
        {
            public Guid? Id { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public Guid ProductId { get; set; }
            public decimal Quantity { get; set; }
            public string? Note { get; set; }
            public string Source { get; set; } = DamageSource.Movement;
        }

        public static class DamageSource
        {
            public const string Movement = "movement";
            public const string ReturnItem = "returnItem";
        }
    }
}
