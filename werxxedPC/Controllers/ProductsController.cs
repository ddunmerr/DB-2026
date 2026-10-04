using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using werxxedPC.Data;

namespace werxxedPC.Controllers
{
    public class ProductsController : Controller
    {
        private readonly AppDbContext _db;

        public ProductsController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /Products
        public async Task<IActionResult> Index(int? categoryId, string? search)
        {
            var query = _db.Products
                .Include(p => p.Category)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Title.Contains(search)
                                      || p.Brand!.Contains(search));

            var products = await query.ToListAsync();
            return View(products);
        }

        // GET: /Products/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var product = await _db.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            return View(product);
        }
    }
}