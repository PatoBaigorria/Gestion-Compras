using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Gestion_Compras.Filters
{
    public class UserNameFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.Controller is Controller controller)
            {
                var userName = context.HttpContext.User.Identity.Name; // Obtén el nombre del usuario
                controller.ViewBag.UserName = userName;

                Console.WriteLine($"UserNameFilter: Usuario autenticado - {userName}");
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}