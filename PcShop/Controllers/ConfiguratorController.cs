using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;
using PcShop.Models.Entities;
using PcShop.Models.ViewModels;
using PcShop.Services;
using System.Text.Json;

namespace PcShop.Controllers;

public class ConfiguratorController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICompatibilityService _compat;

    public ConfiguratorController(AppDbContext db, ICompatibilityService compat)
    {
        _db = db;
        _compat = compat;
    }

    // Главная страница конфигуратора
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var build = await GetOrCreateActiveBuildAsync(userId);

        var items = await _db.BuildItems
            .Where(bi => bi.BuildId == build.Id)
            .Include(bi => bi.Product)
            .ThenInclude(p => p.Category)
            .ToListAsync();

        var categories = await _db.Categories
            .Where(c => c.ParentId != null) // только дочерние (Комплектующие)
            .ToListAsync();

        var vm = new ConfiguratorViewModel
        {
            Build = build,
            Items = items,
            Categories = categories,
            TotalPrice = items.Sum(i => i.Product!.Price * i.Quantity),
            TotalTdp = items.Sum(i => GetTdp(i.Product!))
        };

        return View(vm);
    }

    // AJAX: получить совместимые товары категории
    [HttpGet]
    public async Task<IActionResult> GetCompatible(int categoryId)
    {
        var userId = GetCurrentUserId();
        var build = await GetOrCreateActiveBuildAsync(userId);

        var currentProducts = await _db.BuildItems
            .Where(bi => bi.BuildId == build.Id)
            .Include(bi => bi.Product)
            .Select(bi => bi.Product!)
            .ToListAsync();

        var compatible = await _compat.GetCompatibleProductsAsync(categoryId, currentProducts);

        return Json(compatible.Select(p => new
        {
            p.Id,
            p.Name,
            p.Brand,
            p.Price,
            p.ImageUrl,
            p.Stock
        }));
    }

    // Добавить компонент в сборку
    [HttpPost]
    public async Task<IActionResult> AddItem(int productId)
    {
        var userId = GetCurrentUserId();
        var build = await GetOrCreateActiveBuildAsync(userId);
        var product = await _db.Products.FindAsync(productId);

        if (product == null)
            return NotFound();

        // Определяем категорию товара и удаляем старый товар этой же категории (замена)
        var existingItem = await _db.BuildItems
            .Include(bi => bi.Product)
            .FirstOrDefaultAsync(bi => bi.BuildId == build.Id
                                    && bi.Product!.CategoryId == product.CategoryId);

        if (existingItem != null)
            _db.BuildItems.Remove(existingItem);

        _db.BuildItems.Add(new BuildItem
        {
            BuildId = build.Id,
            ProductId = productId,
            Quantity = 1
        });

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // Удалить компонент
    [HttpPost]
    public async Task<IActionResult> RemoveItem(int productId)
    {
        var userId = GetCurrentUserId();
        var build = await GetOrCreateActiveBuildAsync(userId);

        var item = await _db.BuildItems
            .FirstOrDefaultAsync(bi => bi.BuildId == build.Id && bi.ProductId == productId);

        if (item != null)
        {
            _db.BuildItems.Remove(item);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // Оформить заказ из сборки
    [HttpPost]
    public async Task<IActionResult> Checkout()
    {
        var userId = GetCurrentUserId();
        var build = await GetOrCreateActiveBuildAsync(userId);

        var items = await _db.BuildItems
            .Where(bi => bi.BuildId == build.Id)
            .Include(bi => bi.Product)
            .ToListAsync();

        if (!items.Any())
        {
            TempData["Error"] = "Сборка пуста";
            return RedirectToAction(nameof(Index));
        }

        // Проверяем совместимость
        var errors = await _compat.ValidateBuildAsync(items.Select(i => i.Product!));
        if (errors.Any())
        {
            TempData["Error"] = string.Join("; ", errors);
            return RedirectToAction(nameof(Index));
        }

        // Создаем заказ
        var order = new Order
        {
            CustomerId = userId,
            Status = "new",
            CreatedAt = DateTime.UtcNow
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Копируем позиции в заказ с фиксацией цены
        foreach (var item in items)
        {
            _db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                PriceAtPurchase = item.Product!.Price
            });
        }

        // Очищаем сборку
        _db.BuildItems.RemoveRange(items);
        await _db.SaveChangesAsync();

        return RedirectToAction("Details", "Order", new { id = order.Id });
    }

    // ---------- Вспомогательные ----------

    private int GetCurrentUserId()
    {
        // В реальном приложении берем из Claims
        return HttpContext.Session.GetInt32("UserId")
            ?? throw new UnauthorizedAccessException();
    }

    private async Task<Build> GetOrCreateActiveBuildAsync(int userId)
    {
        var build = await _db.Builds
            .FirstOrDefaultAsync(b => b.CustomerId == userId && b.IsActive);

        if (build == null)
        {
            build = new Build { CustomerId = userId, IsActive = true };
            _db.Builds.Add(build);
            await _db.SaveChangesAsync();
        }
        return build;
    }

    private int GetTdp(Product p)
    {
        if (p.Specs == null)
            return 0;
        using var doc = JsonDocument.Parse(p.Specs);
        return doc.RootElement.TryGetProperty("tdp", out var v) ? v.GetInt32() : 0;
    }
}