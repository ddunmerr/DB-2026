using PcShop.Models.Entities;

namespace PcShop.Models.ViewModels;

public class ConfiguratorViewModel
{
    public Build Build { get; set; } = null!;
    public List<BuildItem> Items { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public int TotalTdp { get; set; }
    public List<string> Warnings { get; set; } = new();

}
