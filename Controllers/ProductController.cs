using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;

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
            .Select(r => new
            {
                r.Id,
                r.Rating,
                r.Text,
                r.CreatedAt,
                AuthorName = r.Customer!.Name,
                r.CustomerId
            })
            .ToListAsync();

        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var userReview = reviews.FirstOrDefault(r => r.CustomerId == currentUserId);

        ViewBag.Product = product;
        ViewBag.Reviews = reviews;
        ViewBag.UserReview = userReview;
        ViewBag.AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

        return View();
    }
}