using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Gestion_Compras.Controllers
{
    [Authorize]
    public class PedidoViewController : Controller
    {
        [Authorize(Roles = "Administrador,Pañolero")]
        public IActionResult Nuevo()
        {
            return View("~/Views/Pedido/Index.cshtml");
        }

        public IActionResult Lista()
        {
            return View("~/Views/Pedido/Lista.cshtml");
        }
    }
}
