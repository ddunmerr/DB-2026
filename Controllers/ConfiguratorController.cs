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

        // Получаем текущие товары (для RAM не исключаем, т.к. можно добавить несколько)
        var currentProducts = await _db.BuildItems
            .Where(bi => bi.BuildId == build.Id)
            .Include(bi => bi.Product)
            .Where(bi => bi.Product != null && (product.CategoryId == 4 || bi.Product.CategoryId != product.CategoryId))
            .Select(bi => bi.Product!)
            .ToListAsync();

        // Проверка совместимости
        var reason = await _compat.GetIncompatibilityReasonAsync(product, currentProducts);
        if (reason != null)
        {
            TempData["Error"] = $"Нельзя добавить «{product.Name}»: {reason}";
            return RedirectToAction(nameof(Index));
        }

        // Для RAM: проверяем количество планок vs слотов на MB
        if (product.CategoryId == 4) // RAM
        {
            var mb = currentProducts.FirstOrDefault(p => p.CategoryId == 3);
            if (mb != null)
            {
                var ramSlots = GetRamSlots(mb);
                var currentRamCount = await _db.BuildItems
                    .Where(bi => bi.BuildId == build.Id && bi.Product!.CategoryId == 4)
                    .SumAsync(bi => bi.Quantity);

                if (currentRamCount >= ramSlots)
                {
                    TempData["Error"] = $"На материнской плате только {ramSlots} слота памяти. Больше планок добавить нельзя.";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Добавляем RAM (не удаляем старый, а увеличиваем количество или добавляем новый)
            var existingRam = await _db.BuildItems
                .FirstOrDefaultAsync(bi => bi.BuildId == build.Id && bi.ProductId == productId);

            if (existingRam != null)
            {
                existingRam.Quantity += 1;
            }
            else
            {
                _db.BuildItems.Add(new BuildItem
                {
                    BuildId = build.Id,
                    ProductId = productId,
                    Quantity = 1
                });
            }
        }
        else
        {
            // Для остальных категорий: замена компонента
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
        }

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
            TempData["Error"] = "Сборка пуста. Добавьте компоненты.";
            return RedirectToAction(nameof(Index));
        }

        // 1. Проверка совместимости (критические ошибки)
        var compatErrors = await _compat.ValidateBuildAsync(items.Select(i => i.Product!));
        if (compatErrors.Any())
        {
            TempData["Error"] = "❌ Ошибка совместимости:<br>" + string.Join("<br>", compatErrors);
            return RedirectToAction(nameof(Index));
        }

        // 2. НОВАЯ ПРОВЕРКА: Наличие всех обязательных компонентов
        // ID категорий: 2=CPU, 3=MB, 4=RAM, 6=PSU, 7=Case
        // (Видеокарта (5) не обязательна, если у процессора есть встроенная графика)
        var mandatoryCategoryIds = new List<int> { 2, 3, 4, 6, 7 };
        var selectedCategoryIds = items.Select(i => i.Product!.CategoryId).ToList();

        var missingParts = new List<string>();
        if (!selectedCategoryIds.Contains(2)) missingParts.Add("Процессор");
        if (!selectedCategoryIds.Contains(3)) missingParts.Add("Материнская плата");
        if (!selectedCategoryIds.Contains(4)) missingParts.Add("Оперативная память");
        if (!selectedCategoryIds.Contains(6)) missingParts.Add("Блок питания");
        if (!selectedCategoryIds.Contains(7)) missingParts.Add("Корпус");

        // Если чего-то не хватает, прерываем оформление и показываем ошибку
        if (missingParts.Any())
        {
            var errorMessages = missingParts.Select(p => $"Запчасть \"{p}\" обязательна к выбору");
            TempData["Error"] = "⚠️ Невозможно оформить заказ. Неполная сборка:<br>" + string.Join("<br>", errorMessages);
            return RedirectToAction(nameof(Index));
        }

        // 3. Если всё отлично, создаем Заказ
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

        // Очищаем сборку после успешного заказа
        _db.BuildItems.RemoveRange(items);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Заказ успешно оформлен!";
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

    
    private int GetRamSlots(Product mb)
    {
        if (string.IsNullOrWhiteSpace(mb.Specs)) return 4; // по умолчанию
        using var doc = JsonDocument.Parse(mb.Specs);
        return doc.RootElement.TryGetProperty("ram_slots", out var v) ? v.GetInt32() : 4;
    }
    
    private int GetTdp(Product p)
    {
        if (p.Specs == null)
            return 0;
        using var doc = JsonDocument.Parse(p.Specs);
        return doc.RootElement.TryGetProperty("tdp", out var v) ? v.GetInt32() : 0;
    }
    
    [HttpPost]
    public async Task<IActionResult> DecreaseRamQuantity(int productId)
    {
        var userId = GetCurrentUserId();
        var build = await GetOrCreateActiveBuildAsync(userId);

        var item = await _db.BuildItems
            .FirstOrDefaultAsync(bi => bi.BuildId == build.Id && bi.ProductId == productId);

        if (item != null)
        {
            if (item.Quantity > 1)
            {
                item.Quantity -= 1;
            }
            else
            {
                _db.BuildItems.Remove(item);
            }
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}