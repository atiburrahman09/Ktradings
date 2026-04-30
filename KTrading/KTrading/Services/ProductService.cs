using KTrading.Models;
using KTrading.Repositories;

namespace KTrading.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _repo;

        public ProductService(IProductRepository repo)
        {
            _repo = repo;
        }

        public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            if (product.Id == Guid.Empty) product.Id = Guid.NewGuid();
            product.CreatedAt = DateTimeOffset.UtcNow;
            await _repo.AddAsync(product, cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _repo.GetByIdAsync(id, cancellationToken);
            if (entity is null) return;
            _repo.Remove(entity);
        }

        public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _repo.ListAsync(cancellationToken);
        }

        public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _repo.GetByIdAsync(id, cancellationToken);
        }

        public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            _repo.Update(product);
            await Task.CompletedTask;
        }
    }
}
