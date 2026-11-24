using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;
using PcShop.Models.Entities;
using PcShop.Models.ViewModels;

namespace PcShop.Controllers;

public class ReviewController : Controller
{
    private readonly AppDbContext _db;

    public ReviewController(AppDbContext db) => _db = db;

    // GET: /Review/Create?productId=5
    public IActionResult Create(int productId)
    {
        var userId = GetCurrentUserId();

        // Проверка: не оставлял ли пользователь уже отзыв на этот товар
        var existingReview = _db.Reviews.Any(r => r.CustomerId == userId && r.ProductId == productId);
        if (existingReview)
        {
            TempData["Error"] = "Вы уже оставили отзыв на этот товар. Вы можете его отредактировать в карточке товара.";
            return RedirectToAction("Details", "Product", new { id = productId });
        }

        var product = _db.Products.Find(productId);
        if (product == null)
            return NotFound();

        var vm = new ReviewEditViewModel // Используем ту же ViewModel
        {
            ProductId = productId,
            ProductName = product.Name,
            Rating = 5 // Оценка по умолчанию
        };

        return View(vm);
    }

    // POST: /Review/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReviewEditViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var userId = GetCurrentUserId();

        // Двойная проверка на уровне БД перед сохранением
        var exists = await _db.Reviews.AnyAsync(r => r.CustomerId == userId && r.ProductId == vm.ProductId);
        if (exists)
        {
            ModelState.AddModelError("", "Вы уже оставили отзыв на этот товар.");
            return View(vm);
        }

        var review = new Review
        {
            CustomerId = userId,
            ProductId = vm.ProductId,
            Rating = vm.Rating,
            Text = vm.Text,
            CreatedAt = DateTime.UtcNow
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Отзыв успешно добавлен!";
        return RedirectToAction("Details", "Product", new { id = vm.ProductId });
    }

    // GET: /Review/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var userId = GetCurrentUserId();

        var review = await _db.Reviews
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (review == null)
            return NotFound();

        // Защита: пользователь может редактировать только свои отзывы
        if (review.CustomerId != userId)
            return Forbid();

        var vm = new ReviewEditViewModel
        {
            Id = review.Id,
            ProductId = review.ProductId,
            ProductName = review.Product?.Name ?? "",
            Rating = review.Rating,
            Text = review.Text
        };

        return View(vm);
    }

    // POST: /Review/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ReviewEditViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var userId = GetCurrentUserId();

        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == vm.Id);
        if (review == null)
            return NotFound();

        // Повторная проверка прав на уровне POST-запроса
        if (review.CustomerId != userId)
            return Forbid();

        review.Rating = vm.Rating;
        review.Text = vm.Text;
        // Дату создания НЕ трогаем — отзыв отредактирован, а не создан заново

        await _db.SaveChangesAsync();

        TempData["Success"] = "Отзыв успешно обновлён";
        return RedirectToAction("Details", "Product", new { id = review.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == id);

        if (review == null)
            return NotFound();
        if (review.CustomerId != userId)
            return Forbid();

        var productId = review.ProductId;
        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Отзыв удалён";
        return RedirectToAction("Details", "Product", new { id = productId });
    }

    private int GetCurrentUserId()
    {
        return HttpContext.Session.GetInt32("UserId");

    }
}