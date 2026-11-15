using System.ComponentModel.DataAnnotations;

namespace PcShop.Models.Entities;

public class Customer
{
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Role { get; set; } = "customer";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Build> Builds { get; set; } = new List<Build>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}