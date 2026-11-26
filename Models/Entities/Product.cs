using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PcShop.Models.Entities;

[Table("Product")]
public class Product
{
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Brand { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    public int Stock { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public string? Description { get; set; }

    public string Specs { get; set; } = "{}";

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
}