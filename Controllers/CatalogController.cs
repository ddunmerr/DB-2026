using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;

namespace PcShop.Controllers;

public class CatalogController : Controller
{
    private readonly AppDbContext _db;

    public CatalogController(AppDbContext db) => _db = db;

    // Главная страница каталога
    public async Task<IActionResult> Index(int? categoryId, string? search, string? brand)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .AsQueryable();

        // Фильтр по категории
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        // Поиск по названию
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.Contains(search) || p.Brand.Contains(search));
        }

        // Фильтр по бренду
        if (!string.IsNullOrWhiteSpace(brand))
        {
            query = query.Where(p => p.Brand == brand);
        }

        var products = await query.OrderBy(p => p.Name).ToListAsync();

        // Получаем список брендов для фильтра
        var brands = await _db.Products
            .Select(p => p.Brand)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();

        // Получаем категории
        var categories = await _db.Categories
            .Where(c => c.ParentId != null)
            .ToListAsync();

        ViewBag.CurrentCategoryId = categoryId;
        ViewBag.CurrentSearch = search;
        ViewBag.CurrentBrand = brand;
        ViewBag.Brands = brands;
        ViewBag.Categories = categories;

        return View(products);
    }

    // Карточка товара
    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Reviews)
            .ThenInclude(r => r.Customer)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
            return NotFound();

        // История цен для графика
        var priceHistory = await _db.PriceHistories
            .Where(ph => ph.ProductId == id)
            .OrderBy(ph => ph.ChangedAt)
            .ToListAsync();

        ViewBag.PriceHistory = priceHistory;

        return View(product);
    }
}