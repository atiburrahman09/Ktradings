using System.ComponentModel.DataAnnotations;

namespace KTrading.Models
{
    public class SalesOfficer
    {
        public Guid Id { get; set; }

        [Required, MaxLength(250)]
        public string Name { get; set; } = null!;

        [MaxLength(50)]
        public string? Code { get; set; }

        public string? Phone { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
