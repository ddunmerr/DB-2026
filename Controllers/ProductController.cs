using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;
using PcShop.Models.ViewModels;
using System.Security.Claims;

namespace PcShop.Controllers;

public class ProductController : Controller
{
    private readonly AppDbContext _db;
    public ProductController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
            return NotFound();

        var reviews = await _db.Reviews
            .Where(r => r.ProductId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewItemViewModel
            {
                Id = r.Id,
                Rating = r.Rating,
                Text = r.Text,
                CreatedAt = r.CreatedAt,
                AuthorName = r.Customer!.Name,
                CustomerId = r.CustomerId
            })
            .ToListAsync();

        var priceHistory = await _db.PriceHistories
            .Where(ph => ph.ProductId == id)
            .OrderBy(ph => ph.ChangedAt)
            .ToListAsync();

        int? currentUserId = null;
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idClaim, out var cid))
            currentUserId = cid;
        else
            currentUserId = HttpContext.Session.GetInt32("UserId");

        var vm = new ProductDetailsViewModel
        {
            Product = product,
            Reviews = reviews,
            UserReview = reviews.FirstOrDefault(r => r.CustomerId == currentUserId),
            AverageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0,
            PriceHistory = priceHistory
        };

        return View(vm);
    }
}