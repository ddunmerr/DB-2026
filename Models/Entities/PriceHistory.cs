using System.ComponentModel.DataAnnotations.Schema;

namespace PcShop.Models.Entities;

[Table("PriceHistory")]
public class PriceHistory
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}