using System.ComponentModel.DataAnnotations;

namespace KTrading.Models
{
    public class ProductReturn
    {
        public Guid Id { get; set; }

        [MaxLength(100)]
        public string ReturnNumber { get; set; } = null!;

        public Guid? CustomerId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [MaxLength(1000)]
        public string? Reason { get; set; }

        // e.g. Open, Processed, Closed
        [MaxLength(50)]
        public string Status { get; set; } = "Open";

        public ICollection<ProductReturnItem>? Items { get; set; }
    }
}
