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
            // Validaciones básicas
            if (proveedor == null)
            {
                return BadRequest(new { error = "Los datos del proveedor no pueden estar vacíos." });
            }

            // Validar campos requeridos
            if (string.IsNullOrWhiteSpace(proveedor.RazonSocial))
            {
                return BadRequest(new { error = "La razón social del proveedor no puede estar vacía." });
            }

            if (string.IsNullOrWhiteSpace(proveedor.CUIT))
            {
                return BadRequest(new { error = "El CUIT del proveedor no puede estar vacío." });
            }

            // Validar formato de CUIT (opcional)
            if (!EsCuitValido(proveedor.CUIT))
            {
                return BadRequest(new { error = "El formato del CUIT no es válido. Debe tener el formato XX-XXXXXXXX-X" });
            }

            // Verificar si ya existe un proveedor con la misma razón social o CUIT
            var proveedorExistente = await context.Proveedor
                .FirstOrDefaultAsync(p =>
                    p.RazonSocial == proveedor.RazonSocial ||
                    p.CUIT == proveedor.CUIT);

            if (proveedorExistente != null)
            {
                if (proveedorExistente.RazonSocial == proveedor.RazonSocial)
                {
                    return Conflict(new { error = "Ya existe un proveedor con la misma razón social." });
                }
                else
                {
                    return Conflict(new { error = "Ya existe un proveedor con el mismo CUIT." });
                }
            }

            try
            {
                context.Proveedor.Add(proveedor);
                await context.SaveChangesAsync();
                return Ok(new
                {
                    message = "Proveedor creado exitosamente.",
                    proveedor = new
                    {
                        proveedor.Id,
                        proveedor.RazonSocial,
                        proveedor.NombreComercial,
                        proveedor.CUIT
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Error interno del servidor al crear el proveedor",
                    detalles = ex.Message
                });
            }
        }

        // Método auxiliar para validar formato de CUIT (opcional)
        private bool EsCuitValido(string cuit)
        {
            // Expresión regular para validar formato XX-XXXXXXXX-X
            var regex = new System.Text.RegularExpressions.Regex(@"^\d{2}-\d{8}-\d{1}$");
            return regex.IsMatch(cuit);
        }

        // GET: /Proveedor/Editar/5
        [HttpGet("Editar/{id}")]
        public async Task<IActionResult> Editar(int id)
        {
            try 
            {
                Console.WriteLine($"Solicitando edición del proveedor con ID: {id}");
                var proveedor = await context.Proveedor.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                
                if (proveedor == null)
                {
                    Console.WriteLine($"No se encontró el proveedor con ID: {id}");
                    return NotFound();
                }

                Console.WriteLine($"Datos del proveedor a editar - ID: {proveedor.Id}, RazonSocial: {proveedor.RazonSocial}");
                
                // Asegurarse de que la vista se está devolviendo correctamente
                var viewPath = Path.Combine("Views", "Proveedor", "Editar.cshtml");
                Console.WriteLine($"Intentando devolver la vista: {viewPath}");
                
                // Devolver la vista con el modelo
                return View("Editar", proveedor);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cargar el proveedor: {ex.Message}");
                return StatusCode(500, new { error = "Error al cargar los datos del proveedor", details = ex.Message });
            }
        }

        // PUT: /Proveedor/Editar/5
        [HttpPut("Editar/{id}")]
        public async Task<IActionResult> Editar(int id, [FromBody] Proveedor proveedorActualizado)
        {
            if (id != proveedorActualizado.Id)
            {
                return BadRequest(new { error = "ID del proveedor no coincide." });
            }

            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(proveedorActualizado.RazonSocial))
            {
                return BadRequest(new { error = "La razón social del proveedor no puede estar vacía." });
            }

            if (string.IsNullOrWhiteSpace(proveedorActualizado.CUIT))
            {
                return BadRequest(new { error = "El CUIT del proveedor no puede estar vacío." });
            }

            if (!EsCuitValido(proveedorActualizado.CUIT))
            {
                return BadRequest(new { error = "El formato del CUIT no es válido. Debe tener el formato XX-XXXXXXXX-X" });
            }

            // Verificar si ya existe otro proveedor con la misma razón social o CUIT
            var proveedorExistente = await context.Proveedor
                .FirstOrDefaultAsync(p => 
                    (p.RazonSocial == proveedorActualizado.RazonSocial || 
                     p.CUIT == proveedorActualizado.CUIT) && 
                    p.Id != id);

            if (proveedorExistente != null)
            {
                if (proveedorExistente.RazonSocial == proveedorActualizado.RazonSocial)
                {
                    return Conflict(new { error = "Ya existe otro proveedor con la misma razón social." });
                }
                else
                {
                    return Conflict(new { error = "Ya existe otro proveedor con el mismo CUIT." });
                }
            }

            try
            {
                var proveedor = await context.Proveedor.FindAsync(id);
                if (proveedor == null)
                {
                    return NotFound(new { error = "Proveedor no encontrado." });
                }

                // Actualizar los campos
                proveedor.RazonSocial = proveedorActualizado.RazonSocial;
                proveedor.NombreComercial = proveedorActualizado.NombreComercial;
                proveedor.CUIT = proveedorActualizado.CUIT;

                context.Proveedor.Update(proveedor);
                await context.SaveChangesAsync();

                return Ok(new { 
                    message = "Proveedor actualizado exitosamente.",
                    proveedor = new {
                        proveedor.Id,
                        proveedor.RazonSocial,
                        proveedor.NombreComercial,
                        proveedor.CUIT
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Error al actualizar el proveedor", 
                    detalles = ex.Message 
                });
            }
        }

        // DELETE: /Proveedor/Delete/5
        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var proveedor = await context.Proveedor.FindAsync(id);
                if (proveedor == null)
                {
                    return NotFound(new { error = "Proveedor no encontrado." });
                }

                // Verificar si el proveedor tiene ingresos asociados
                bool tieneIngresos = await context.Ingreso.AnyAsync(i => i.ProveedorId == id);

                if (tieneIngresos)
                {
                    return BadRequest(new { error = "No se puede eliminar el proveedor porque tiene registros de ingreso asociados." });
                }

                context.Proveedor.Remove(proveedor);
                await context.SaveChangesAsync();
                return Ok(new { message = "Proveedor eliminado exitosamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al eliminar el proveedor", detalles = ex.Message });
            }
        }
    }
}
