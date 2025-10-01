using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Gestion_Compras.Controllers
{
    [Authorize]
    public class StockHistoricoViewController : Controller
    {
        // GET: /StockHistorico/Index
        [HttpGet("/StockHistorico")]
        [HttpGet("/StockHistorico/Index")]
        public IActionResult Index()
        {
            ViewBag.UserName = User.Identity.Name;
            return View("~/Views/StockHistorico/Index.cshtml");
        }
    }
}
