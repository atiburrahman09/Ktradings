using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KTrading.Models
{
    public class Product
    {
        [Key]
        public Guid Id { get; set; }

        [MaxLength(100)]
        public string? SKU { get; set; }

        [Required, MaxLength(500)]
        public string Name { get; set; } = null!;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Unit { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Cost { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal VATPercent { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // New relationship to category
        public Guid? ProductCategoryId { get; set; }
        public ProductCategory? ProductCategory { get; set; }
    }
}
