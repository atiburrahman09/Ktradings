using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KTrading.Models
{
    public class ProductUnit
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }

        [MaxLength(50)]
        public string Name { get; set; } = "";

        [Column(TypeName = "decimal(18,4)")]
        public decimal ConversionFactor { get; set; } = 1m;

        [Column(TypeName = "decimal(18,4)")]
        public decimal SellingPrice { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal PurchaseCost { get; set; }

        [MaxLength(100)]
        public string? SKU { get; set; }

        public bool IsBaseUnit { get; set; }
        public bool IsActive { get; set; } = true;
        public Product? Product { get; set; }
    }
}
