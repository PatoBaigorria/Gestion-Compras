using Microsoft.AspNetCore.Mvc;

namespace Gestion_Compras.Controllers
{
    public class HomeController : Controller
    {
        // GET: /Home/Index
        public IActionResult Index()
        {
            ViewBag.UserName = User.Identity.Name; // Establecer el nombre del usuario en el ViewBag
            return View();
        }

        // GET: /Home/Error
        public IActionResult Error()
        {
            ViewBag.UserName = User.Identity.Name; // Establecer el nombre del usuario en el ViewBag
            return View();
        }

    }
}
