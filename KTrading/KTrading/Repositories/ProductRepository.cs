using KTrading.Data;
using KTrading.Models;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Repositories
{
    public class ProductRepository : RepositoryBase<Product>, IProductRepository
    {
        public ProductRepository(ApplicationDbContext db) : base(db)
        {
        }

        public async Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sku))
                return null;

            return await _db.Products.FirstOrDefaultAsync(p => p.SKU == sku, cancellationToken);
        }
    }
}
