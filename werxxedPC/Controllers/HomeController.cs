using Microsoft.AspNetCore.Mvc;
using werxxedPC.Data;

namespace werxxedPC.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            var count = _db.Products.Count();
            ViewBag.ProductCount = count;
            return View();
        }
    }
}