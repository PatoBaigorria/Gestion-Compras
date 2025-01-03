using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;

namespace Gestion_Compras.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ProveedorController : Controller
    {
        private readonly DataContext context;

        public ProveedorController(DataContext context)
        {
            this.context = context;
        }

        // GET: /Proveedor
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var proveedores = await context.Proveedor.ToListAsync();
            return View(proveedores); // Pasa la lista a la vista Razor
        }

        // GET: /Proveedor/Alta
        [HttpGet("Alta")]
        public IActionResult Alta()
        {
            return View("Alta"); // Devuelve la vista para crear un proveedor
        }

        // POST: /Proveedor/Create 
        [HttpPost("Create")] 
        public async Task<ActionResult> Create([FromBody] Proveedor proveedor) 
        { 
            if (proveedor == null || string.IsNullOrWhiteSpace(proveedor.RazonSocial)) 
            { 
                return BadRequest(new { error = "La razón social del proveedor no puede estar vacía." }); 
            } 
            var proveedorExistente = await context.Proveedor.FirstOrDefaultAsync(p => p.RazonSocial == proveedor.RazonSocial); 
            if (proveedorExistente != null) 
            { 
                return Conflict(new { error = "El proveedor ya existe." }); 
            }

            try 
            { 
                context.Proveedor.Add(proveedor); 
                await context.SaveChangesAsync(); 
                return Ok(new { message = "Proveedor creado exitosamente.", proveedor }); 
            } 
            catch (Exception ex) 
            { 
                return StatusCode(500, new { error = "Error interno del servidor", detalles = ex.Message }); 
            } 
        }
    }
}
