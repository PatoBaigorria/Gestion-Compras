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
            Console.WriteLine($"Intento de login - Usuario: {username}");
            var usuario = context.Usuario.SingleOrDefault(u => u.UsuarioLogin == username);
            
            if (usuario != null)
            {
                Console.WriteLine($"Usuario encontrado: {usuario.UsuarioLogin}");
                Console.WriteLine($"Password en BD: {usuario.Password}");
                Console.WriteLine($"Password ingresada: {password}");
                
                // Verificar si el usuario está activo para login
                // Si no está activo pero es su primer inicio, permitir el cambio de contraseña
                if (!usuario.ActivarLogin && usuario.PrimeraVezLogin != 1)
                {
                    Console.WriteLine("Error: El usuario no está activo para iniciar sesión");
                    TempData["ErrorType"] = "Cuenta inactiva";
                    TempData["ErrorMessage"] = "Tu cuenta no está activa. Por favor, contacta al administrador del sistema.";
                    return View("Login");
                }
                
                // Verificar la contraseña
                Console.WriteLine("Verificando contraseña...");
                bool passwordValida = VerificarPassword(password, usuario.Password);
                Console.WriteLine($"Resultado verificación: {passwordValida}");
                
                if (!passwordValida)
                {
                    Console.WriteLine("Error: Contraseña incorrecta");
                    TempData["ErrorType"] = "Credenciales inválidas";
                    TempData["ErrorMessage"] = "El usuario o la contraseña son incorrectos. Por favor, verifica e intenta nuevamente.";
                    return View("Login");
                }
                
                if (passwordValida)
                {
                    // Si es la primera vez que inicia sesión, redirigir a cambio de contraseña
                    if (usuario.PrimeraVezLogin == 1)
                    {
                        Console.WriteLine($"Redirigiendo a cambio de contraseña. ID: {usuario.Id}, PrimeraVez: {usuario.PrimeraVezLogin}");
                        
                        // Autenticar al usuario primero
                        var userClaims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, usuario.UsuarioLogin),
                            new Claim(ClaimTypes.GivenName, usuario.Nombre ?? string.Empty),
                            new Claim(ClaimTypes.Surname, usuario.Apellido ?? string.Empty),
                            new Claim(ClaimTypes.Role, usuario.RolNombre ?? "Usuario"),
                            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                        };

                        var userClaimsIdentity = new ClaimsIdentity(userClaims, CookieAuthenticationDefaults.AuthenticationScheme);
                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(userClaimsIdentity));
                        
                        // Generar URL absoluta para asegurar que la redirección funcione
                        var url = Url.Action("CambiarPassword", "Autenticacion", 
                            new { id = usuario.Id, primeraVez = 1 });
                            
                        Console.WriteLine($"URL de redirección: {url}");
                        return Redirect(url);
                    }
                    
                    // Si el usuario no está activo, no permitir el acceso
                    if (!usuario.ActivarLogin)
                    {
                        Console.WriteLine("Error: El usuario no está activo");
                        TempData["ErrorType"] = "Cuenta inactiva";
                        TempData["ErrorMessage"] = "Tu cuenta no está activa. Por favor, contacta al administrador del sistema.";
                        return View("Login");
                    }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, usuario.UsuarioLogin),
                    new Claim(ClaimTypes.GivenName, usuario.Nombre),
                    new Claim(ClaimTypes.Surname, usuario.Apellido),
                    new Claim(ClaimTypes.Role, usuario.RolNombre),
                    new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
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
            }
            else
            {
                Console.WriteLine($"Usuario NO encontrado: {username}");
            }

            Console.WriteLine("Login fallido - credenciales incorrectas");
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

        [AllowAnonymous]
        [HttpGet("CambiarPassword")]
        public IActionResult CambiarPassword(int? id, int primeraVez = 0)
        {
            Console.WriteLine($"GET CambiarPassword - ID: {id}, PrimeraVez: {primeraVez}");
            if (!id.HasValue)
            {
                Console.WriteLine("Error: No se proporcionó ID de usuario");
                return RedirectToAction("Login");
            }
            
            var usuario = context.Usuario.Find(id.Value);
            if (usuario == null)
            {
                Console.WriteLine($"Error: No se encontró usuario con ID {id}");
                return RedirectToAction("Login");
            }
            
            // Si es la primera vez, no pedir la contraseña actual
            ViewBag.EsPrimeraVez = (primeraVez == 1);
            return View(usuario);
        }

        [AllowAnonymous]
        [HttpPost("CambiarPassword")]
        public async Task<IActionResult> CambiarPassword(int id, string currentPassword, string newPassword, int primeraVez = 0)
        {
            Console.WriteLine($"POST CambiarPassword - ID: {id}, PrimeraVez: {primeraVez}");
            var usuario = await context.Usuario.FindAsync(id);
            if (usuario == null)
            {
                ModelState.AddModelError("", "Usuario no encontrado");
                return View(usuario);
            }

            // Verificar contraseña actual
            if (!VerificarPassword(currentPassword, usuario.Password))
            {
                ModelState.AddModelError("currentPassword", "La contraseña actual es incorrecta");
                return View(usuario);
            }

            // Validar que la nueva contraseña sea diferente a la actual
            if (VerificarPassword(newPassword, usuario.Password))
            {
                ModelState.AddModelError("newPassword", "La nueva contraseña debe ser diferente a la actual");
                return View(usuario);
            }

            try
            {
                // Si no es la primera vez, verificar la contraseña actual
                if (primeraVez != 1 && !VerificarPassword(currentPassword, usuario.Password))
                {
                    ModelState.AddModelError("currentPassword", "La contraseña actual es incorrecta");
                    ViewBag.EsPrimeraVez = false; // Mostrar campo de contraseña actual
                    return View(usuario);
                }

                usuario.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
                usuario.ActivarLogin = false;
                usuario.PrimeraVezLogin = 0; // 0 = false
                context.Update(usuario);
                await context.SaveChangesAsync();
                
                // Cerrar la sesión actual
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                
                // Redirigir al login con mensaje de éxito
                TempData["MensajeExito"] = "Contraseña cambiada exitosamente. Por favor inicie sesión nuevamente.";
                return RedirectToAction("Login", "Autenticacion");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Ocurrió un error al cambiar la contraseña: " + ex.Message);
                return View(usuario);
            }
        }

        // Método auxiliar para verificar contraseñas con manejo de errores
        private bool VerificarPassword(string password, string hash)
        {
            try
            {
                if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
                {
                    Console.WriteLine("Error: Contraseña o hash vacío");
                    return false;
                }

                Console.WriteLine($"Hash a verificar: {hash}");
                Console.WriteLine($"Longitud del hash: {hash?.Length}");
                Console.WriteLine($"¿El hash comienza con $2a$?: {hash?.StartsWith("$2a$")}");
                
                Console.WriteLine("Verificando contraseña con BCrypt...");
                bool esValida = BCrypt.Net.BCrypt.Verify(password, hash);
                
                // Si falla, intentar con un hash generado con la misma contraseña para comparar
                if (!esValida)
                {
                    Console.WriteLine("La verificación falló, generando un nuevo hash para comparar...");
                    string nuevoHash = BCrypt.Net.BCrypt.HashPassword(password);
                    Console.WriteLine($"Nuevo hash generado: {nuevoHash}");
                    Console.WriteLine($"¿Los hashes son iguales?: {hash == nuevoHash}");
                }
                
                Console.WriteLine($"Resultado de verificación BCrypt: {esValida}");
                return esValida;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al verificar contraseña: {ex.Message}");
                Console.WriteLine($"Tipo de excepción: {ex.GetType().FullName}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }
    }
}
