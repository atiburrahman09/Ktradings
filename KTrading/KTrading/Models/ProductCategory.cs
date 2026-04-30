using System.ComponentModel.DataAnnotations;

namespace KTrading.Models
{
    public class ProductCategory
    {
        public Guid Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = null!;

        public ICollection<Product>? Products { get; set; }
    }
}
