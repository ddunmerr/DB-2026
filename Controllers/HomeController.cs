using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;

namespace PcShop.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        // Показываем популярные категории
        var categories = await _db.Categories
            .Where(c => c.ParentId != null)
            .ToListAsync();

        ViewBag.Categories = categories;
        return View();
    }
}