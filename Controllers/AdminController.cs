using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PcShop.Data;
using PcShop.Models.Entities;
using PcShop.Models.ViewModels;

namespace PcShop.Controllers;

[Authorize(Roles = "admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public AdminController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // 1. СПИСОК ТОВАРОВ (с поиском и сортировкой)
    public async Task<IActionResult> Index(string? search, string? sortBy = "name")
    {
        var query = _db.Products.Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Brand.Contains(search));

        query = sortBy switch
        {
            "price_asc" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            "stock" => query.OrderByDescending(p => p.Stock),
            _ => query.OrderBy(p => p.Name)
        };

        ViewBag.Search = search;
        ViewBag.SortBy = sortBy;
        return View(await query.ToListAsync());
    }

    // 2. СОЗДАНИЕ / РЕДАКТИРОВАНИЕ ТОВАРА
    public async Task<IActionResult> Edit(int? id)
    {
        ViewBag.Categories = new SelectList(await _db.Categories.Where(c => c.ParentId != null).ToListAsync(), "Id", "Title");

        if (id == null) return View(new ProductEditViewModel());

        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        return View(new ProductEditViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Brand = product.Brand,
            Price = product.Price,
            Stock = product.Stock,
            CategoryId = product.CategoryId,
            CurrentImageUrl = product.ImageUrl,
            Specs = product.Specs,
            Description = product.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = new SelectList(await _db.Categories.Where(c => c.ParentId != null).ToListAsync(), "Id", "Title", model.CategoryId);
            return View(model);
        }

        Product product;
        if (model.Id == 0) // Создание
        {
            product = new Product { CreatedAt = DateTime.UtcNow };
            _db.Products.Add(product);
        }
        else // Редактирование
        {
            product = await _db.Products.FindAsync(model.Id);
            if (product == null) return NotFound();
            product.UpdatedAt = DateTime.UtcNow;
        }

        product.Name = model.Name;
        product.Brand = model.Brand;
        product.Price = model.Price;
        product.Stock = model.Stock;
        product.CategoryId = model.CategoryId;
        product.Specs = model.Specs;
        product.Description = model.Description;

        // 🖼️ ОБРАБОТКА ИЗОБРАЖЕНИЯ
        if (model.ImageFile != null)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "products");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.ImageFile.CopyToAsync(stream);
            }

            product.ImageUrl = "/images/products/" + fileName;
        }
        else if (string.IsNullOrEmpty(product.ImageUrl))
        {
            product.ImageUrl = "/images/no-image.png"; // Заглушка, если картинки нет
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // 3. УДАЛЕНИЕ ТОВАРА
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product != null)
        {
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    // 4. УПРАВЛЕНИЕ ЗАКАЗАМИ
    public async Task<IActionResult> Orders()
    {
        var orders = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeOrderStatus(int orderId, string status)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order != null)
        {
            order.Status = status;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Orders));
    }

    // 5.ДАШБОРД 
    public async Task<IActionResult> Dashboard()
    {
        var totalRevenue = await _db.OrderItems.SumAsync(oi => (decimal?)oi.PriceAtPurchase * oi.Quantity) ?? 0;
        var totalOrders = await _db.Orders.CountAsync();
    
        // Группируем только по скалярным полям (Id и Name), чтобы избежать ошибки с JSON
        var topProducts = await _db.OrderItems
            .GroupBy(oi => new { oi.ProductId, oi.Product.Name })
            .Select(g => new {
                ProductName = g.Key.Name,
                TotalSold = g.Sum(oi => oi.Quantity),
                Revenue = g.Sum(oi => oi.PriceAtPurchase * oi.Quantity)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToListAsync();

        ViewBag.TotalRevenue = totalRevenue;
        ViewBag.TotalOrders = totalOrders;
        ViewBag.TopProducts = topProducts;

        return View();
    }
}