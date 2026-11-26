using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;
using PcShop.Models.ViewModels;
using System.Security.Claims;

namespace PcShop.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly AppDbContext _db;

    public OrderController(AppDbContext db) => _db = db;

    // Страница конкретного заказа (куда попадаем после оформления)
    public async Task<IActionResult> Details(int id)
    {
        var userId = GetCurrentUserId();

        var order = await _db.Orders
            .Include(o => o.Items)
            .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == userId);

        if (order == null)
            return NotFound();

        var vm = new OrderDetailsViewModel
        {
            Order = order,
            TotalAmount = order.Items.Sum(oi => oi.PriceAtPurchase * oi.Quantity)
        };

        return View(vm);
    }

    // История заказов пользователя
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();

        var orders = await _db.Orders
            .Where(o => o.CustomerId == userId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderHistoryViewModel
            {
                Id = o.Id,
                CreatedAt = o.CreatedAt,
                Status = o.Status,
                TotalAmount = o.Items.Sum(oi => oi.PriceAtPurchase * oi.Quantity),
                ItemsCount = o.Items.Sum(oi => oi.Quantity)
            })
            .ToListAsync();

        return View(orders);
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idClaim, out int userIdFromClaim))
            return userIdFromClaim;

        var userIdFromSession = HttpContext.Session.GetInt32("UserId");
        if (userIdFromSession.HasValue)
            return userIdFromSession.Value;

        throw new UnauthorizedAccessException("Пользователь не авторизован.");
    }
}