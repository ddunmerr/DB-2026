using System.ComponentModel.DataAnnotations;

namespace PcShop.Models.Entities;

public class Review
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}