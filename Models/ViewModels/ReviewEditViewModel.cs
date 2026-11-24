using System.ComponentModel.DataAnnotations;

namespace PcShop.Models.ViewModels;

public class ReviewEditViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    [Required, Range(1, 5)]
    public int Rating { get; set; }

    [Required, MaxLength(1000)]
    public string Text { get; set; } = string.Empty;
}