using Microsoft.AspNetCore.Mvc;

namespace Gestion_Compras.Controllers
{
    public class HomeController : Controller
    {
        // GET: /Home/Index
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Home/Error
        public IActionResult Error()
        {
            return View();
        }

    }
}
