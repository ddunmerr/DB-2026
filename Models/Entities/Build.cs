using System.ComponentModel.DataAnnotations.Schema;

namespace PcShop.Models.Entities;

[Table("Build")]
public class Build
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string Name { get; set; } = "Моя новая сборка";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BuildItem> Items { get; set; } = new List<BuildItem>();
}

[Table("BuildItem")]
public class BuildItem
{
    public int Id { get; set; }
    public int BuildId { get; set; }
    public Build? Build { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; } = 1;
    public bool IsLocked { get; set; }
}