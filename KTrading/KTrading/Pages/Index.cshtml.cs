using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<CategoryMetric> SalesByCategory { get; set; } = new();
        public List<CategoryMetric> ReturnsByCategory { get; set; } = new();
        public List<CategoryMetric> DamagesByCategory { get; set; } = new();
        public decimal TotalSalesAmount { get; set; }
        public decimal TotalReturnAmount { get; set; }
        public decimal TotalDamageAmount { get; set; }
        public decimal NetAfterReturnsAndDamage => TotalSalesAmount - TotalReturnAmount - TotalDamageAmount;
        public int TodayOrderCount { get; set; }
        public int TodayReturnCount { get; set; }

        public async Task OnGetAsync()
        {
            var now = DateTimeOffset.Now;
            var todayStart = new DateTimeOffset(now.Date, now.Offset);
            var tomorrowStart = todayStart.AddDays(1);

            var products = await _db.Products
                .Include(p => p.ProductCategory)
                .ToDictionaryAsync(p => p.Id);

            var todayOrderIds = await _db.SalesOrders
                .Where(o => o.OrderDate >= todayStart && o.OrderDate < tomorrowStart)
                .Select(o => o.Id)
                .ToListAsync();
            TodayOrderCount = todayOrderIds.Count;

            var todaySalesItems = await _db.SalesOrderItems
                .Where(i => todayOrderIds.Contains(i.SalesOrderId))
                .ToListAsync();
            SalesByCategory = BuildCategoryMetrics(
                todaySalesItems.Select(i => new MetricSource(i.ProductId, i.Quantity, i.LineTotal)),
                products);
            TotalSalesAmount = SalesByCategory.Sum(r => r.Amount);

            var todayReturns = await _db.ProductReturns
                .Where(r => r.CreatedAt >= todayStart && r.CreatedAt < tomorrowStart)
                .ToListAsync();
            TodayReturnCount = todayReturns.Count;
            var todayReturnIds = todayReturns.Select(r => r.Id).ToHashSet();
            var todayReturnItems = await _db.ProductReturnItems
                .Where(i => todayReturnIds.Contains(i.ProductReturnId))
                .ToListAsync();

            ReturnsByCategory = BuildCategoryMetrics(
                todayReturnItems
                    .Where(i => i.Quantity > 0)
                    .Select(i => new MetricSource(
                        i.ProductId,
                        i.Quantity,
                        i.Quantity * GetProductPrice(products, i.ProductId))),
                products);
            TotalReturnAmount = ReturnsByCategory.Sum(r => r.Amount);

            var damageMovementRows = await _db.StockMovements
                .Where(m => m.CreatedAt >= todayStart
                    && m.CreatedAt < tomorrowStart
                    && m.MovementType == "DAMAGE"
                    && m.Quantity < 0)
                .ToListAsync();
            var movementReturnDamageKeys = damageMovementRows
                .Where(m => m.ReferenceId.HasValue)
                .Select(m => (ReturnId: m.ReferenceId!.Value, m.ProductId))
                .ToHashSet();

            var damagedReturnItems = todayReturnItems
                .Where(i => GetDamagedQuantity(i) > 0)
                .Where(i => !movementReturnDamageKeys.Contains((i.ProductReturnId, i.ProductId)))
                .Select(i =>
                {
                    var damagedQuantity = GetDamagedQuantity(i);
                    return new MetricSource(
                        i.ProductId,
                        damagedQuantity,
                        damagedQuantity * GetProductPrice(products, i.ProductId));
                });
            var damageMovements = damageMovementRows
                .Select(m =>
                {
                    var quantity = Math.Abs(m.Quantity);
                    return new MetricSource(
                        m.ProductId,
                        quantity,
                        quantity * GetProductPrice(products, m.ProductId));
                });

            DamagesByCategory = BuildCategoryMetrics(damageMovements.Concat(damagedReturnItems), products);
            TotalDamageAmount = DamagesByCategory.Sum(r => r.Amount);
        }

        private static List<CategoryMetric> BuildCategoryMetrics(
            IEnumerable<MetricSource> sources,
            IReadOnlyDictionary<Guid, Product> products)
        {
            return sources
                .GroupBy(s => GetCategoryName(products, s.ProductId))
                .Select(g => new CategoryMetric
                {
                    Category = g.Key,
                    Quantity = g.Sum(i => i.Quantity),
                    Amount = g.Sum(i => i.Amount)
                })
                .OrderByDescending(r => r.Amount)
                .ThenBy(r => r.Category)
                .ToList();
        }

        private static string GetCategoryName(IReadOnlyDictionary<Guid, Product> products, Guid productId)
        {
            return products.TryGetValue(productId, out var product)
                ? product.ProductCategory?.Name ?? "Uncategorized"
                : "Uncategorized";
        }

        private static decimal GetProductPrice(IReadOnlyDictionary<Guid, Product> products, Guid productId)
        {
            return products.TryGetValue(productId, out var product) ? product.Price : 0m;
        }

        private static decimal GetDamagedQuantity(ProductReturnItem item)
        {
            return item.DamagedQuantity > 0 ? item.DamagedQuantity : item.IsDamaged ? item.Quantity : 0m;
        }

        private readonly record struct MetricSource(Guid ProductId, decimal Quantity, decimal Amount);

        public class CategoryMetric
        {
            public string Category { get; set; } = "";
            public decimal Quantity { get; set; }
            public decimal Amount { get; set; }
        }
    }
}
