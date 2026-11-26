using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;
using PcShop.Models.Entities;
using PcShop.Models.ViewModels;
using PcShop.Services;
using System.Text.Json;

namespace PcShop.Controllers;

[Authorize]
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
        var warnings = await _compat.GetBottleneckWarningsAsync(
            items.Select(i => i.Product!).Where(p => p != null));

        var vm = new ConfiguratorViewModel
        {
            Build = build,
            Items = items,
            Categories = categories,
            TotalPrice = items.Sum(i => i.Product!.Price * i.Quantity),
            TotalTdp = items.Sum(i => GetTdp(i.Product!)),
            Warnings = warnings
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
            .Where(bi => bi.Product != null)
            .Select(bi => bi.Product!)
            .ToListAsync();

        var productsWithInfo = await _compat.GetProductsWithCompatibilityInfoAsync(categoryId, currentProducts);

        return Json(productsWithInfo.Select(p => new
        {
            id = p.Product.Id,
            name = p.Product.Name,
            brand = p.Product.Brand,
            price = p.Product.Price,
            imageUrl = p.Product.ImageUrl,
            stock = p.Product.Stock,
            isCompatible = p.IsCompatible,
            reason = p.IncompatibilityReason
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
    {
        TempData["Error"] = "Товар не найден";
        return RedirectToAction(nameof(Index));
    }

    if (product.Stock <= 0)
    {
        TempData["Error"] = $"Товар «{product.Name}» отсутствует на складе";
        return RedirectToAction(nameof(Index));
    }

    // Получаем текущие товары (исключая товар той же категории, т.к. мы его заменяем)
    var currentProducts = await _db.BuildItems
        .Where(bi => bi.BuildId == build.Id)
        .Include(bi => bi.Product)
        .Where(bi => bi.Product != null && bi.Product.CategoryId != product.CategoryId)
        .Select(bi => bi.Product!)
        .ToListAsync();

    // 🔒 СЕРВЕРНАЯ ПРОВЕРКА: не дадим добавить несовместимый товар, даже если JS обошли
    var reason = await _compat.GetIncompatibilityReasonAsync(product, currentProducts);
    if (reason != null)
    {
        TempData["Error"] = $"Нельзя добавить «{product.Name}»: {reason}";
        return RedirectToAction(nameof(Index));
    }

    // Замена компонента той же категории (если он уже был в сборке)
    var existingItem = await _db.BuildItems
        .Include(bi => bi.Product)
        .FirstOrDefaultAsync(bi => bi.BuildId == build.Id 
                                && bi.Product != null 
                                && bi.Product.CategoryId == product.CategoryId);

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
        // 1. Сначала пытаемся получить из Claims (стандартный способ при Cookie Auth)
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(idClaim, out int userIdFromClaim))
        {
            return userIdFromClaim;
        }

        // 2. Фолбэк на Session
        var userIdFromSession = HttpContext.Session.GetInt32("UserId");
        if (userIdFromSession.HasValue)
        {
            return userIdFromSession.Value;
        }

        throw new UnauthorizedAccessException("Пользователь не авторизован.");
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