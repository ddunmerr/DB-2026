using PcShop.Models.Entities;

namespace PcShop.Models.ViewModels;

public class ProductDetailsViewModel
{
    public Product Product { get; set; } = null!;
    public List<ReviewItemViewModel> Reviews { get; set; } = new();
    public ReviewItemViewModel? UserReview { get; set; }
    public double AverageRating { get; set; }
    public List<PriceHistory> PriceHistory { get; set; } = new();

}

public class ReviewItemViewModel
{
    public int Id { get; set; }
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
}