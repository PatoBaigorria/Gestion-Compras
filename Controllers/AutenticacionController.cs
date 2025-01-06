using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using BCrypt.Net;
using Gestion_Compras.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Gestion_Compras.Controllers
{
    [Route("Autenticacion")]
    public class AutenticacionController : Controller
    {
        private readonly DataContext context;

        public AutenticacionController(DataContext context)
        {
            this.context = context;
        }

        // Permitir acceso anónimo a la vista de login
        [AllowAnonymous]
        [HttpGet("Login")]
        public IActionResult Login()
        {
            return View();
        }


        // Permitir acceso anónimo al post de login
        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<IActionResult> Login(string username, string password)
        {
            var usuario = context.Usuario.SingleOrDefault(u => u.UsuarioLogin == username);
            if (usuario != null && BCrypt.Net.BCrypt.Verify(password, usuario.Password))
            {
                if (usuario.ActivarLogin)
                {
                    // Redirigir al usuario a la página de cambio de contraseña
                    return RedirectToAction("CambiarPassword", new { id = usuario.Id });
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, usuario.UsuarioLogin),
                    new Claim(ClaimTypes.GivenName, usuario.Nombre),
                    new Claim(ClaimTypes.Surname, usuario.Apellido),
                    new Claim(ClaimTypes.Role, usuario.RolNombre),
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
                // Código para mostrar los claims en la consola 
                foreach (var claim in claims) 
                { 
                    Console.WriteLine($"{claim.Type}: {claim.Value}"); 
                }

                return RedirectToAction("Index", "Home");
            }

            ViewBag.ErrorMessage = "Nombre de usuario o contraseña incorrectos";
            return View();
        }

        // Permitir acceso anónimo a la vista de registro
        [AllowAnonymous]
        [HttpGet("Signup")]
        public IActionResult Signup()
        {
            return View();
        }

        // Permitir acceso anónimo al post de registro
        [AllowAnonymous]
        [HttpPost("Signup")]
        public async Task<IActionResult> Signup(Usuario usuario)
        {
            if (ModelState.IsValid)
            {
                // Hashear la contraseña antes de guardarla
                usuario.Password = BCrypt.Net.BCrypt.HashPassword(usuario.Password);

                context.Usuario.Add(usuario);
                await context.SaveChangesAsync();
                return RedirectToAction("Login", "Autenticacion");
            }
            return View(usuario);
        }

        // Proteger el método de logout
        [HttpGet("Logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Autenticacion");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet("CambiarPassword/{id}")]
        public IActionResult CambiarPassword(int id)
        {
            var usuario = context.Usuario.Find(id);
            if (usuario == null)
            {
                return NotFound();
            }
            return View(usuario);
        }

        [HttpPost("CambiarPassword/{id}")]
        public async Task<IActionResult> CambiarPassword(int id, string newPassword)
        {
            var usuario = context.Usuario.Find(id);
            if (usuario != null)
            {
                usuario.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
                usuario.ActivarLogin = false; // Cambiar a false después de cambiar la contraseña
                context.Update(usuario);
                await context.SaveChangesAsync();
                return RedirectToAction("Login", "Autenticacion");
            }
            return View(usuario);
        }
    }
}
