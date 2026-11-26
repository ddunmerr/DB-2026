using PcShop.Models.Entities;

namespace PcShop.Models.ViewModels;

public class OrderDetailsViewModel
{
    public Order Order { get; set; } = null!;
    public decimal TotalAmount { get; set; }
}

public class OrderHistoryViewModel
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ItemsCount { get; set; }
}