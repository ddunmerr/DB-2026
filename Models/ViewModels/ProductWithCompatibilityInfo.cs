using PcShop.Models.Entities;

namespace PcShop.Models.ViewModels;

public class ProductWithCompatibilityInfo
{
    public Product Product { get; set; } = null!;
    public bool IsCompatible { get; set; }
    public string? IncompatibilityReason { get; set; }
}