using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Gestion_Compras.Controllers
{
    [Authorize]
    public class PedidoViewController : Controller
    {
        [Authorize(Roles = "Administrador")]
        public IActionResult Index()
        {
            return View("~/Views/Pedido/Index.cshtml");
        }

        public IActionResult Lista()
        {
            return View("~/Views/Pedido/Lista.cshtml");
        }
    }
}
