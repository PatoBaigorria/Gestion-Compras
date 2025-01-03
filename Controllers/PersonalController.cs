using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;

namespace Gestion_Compras.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class PersonalController : Controller
    {
        private readonly DataContext context;

        public PersonalController(DataContext context)
        {
            this.context = context;
        }

        // GET: /Personal
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var personas = await context.Personal.ToListAsync();
            return View(personas); // Pasa la lista a la vista Razor
        }

        // GET: /Personal/Alta
        [HttpGet("Alta")]
        public IActionResult Alta()
        {
            return View("Alta"); // Devuelve la vista para crear personal
        }

        // POST: /Personal/Create 
        [HttpPost("Create")] 
        public async Task<ActionResult> Create([FromBody] Personal personal) 
        { 
            if (personal == null || string.IsNullOrWhiteSpace(personal.NombreYApellido)) 
            { 
                return BadRequest(new { error = "El nombre y apellido del personal no pueden estar vacíos." }); 
            } 
            var personalExistente = await context.Personal.FirstOrDefaultAsync(p => p.DNI == personal.DNI); 
            if (personalExistente != null) 
            { 
                return Conflict(new { error = "El personal con ese DNI ya existe." }); 
            }

            try 
            { 
                context.Personal.Add(personal); 
                await context.SaveChangesAsync(); 
                return Ok(new { message = "Personal creado exitosamente.", personal }); 
            } 
            catch (Exception ex) 
            { 
                return StatusCode(500, new { error = "Error interno del servidor", detalles = ex.Message }); 
            } 
        }

        // DELETE: /Personal/Delete/{id} 
        [HttpDelete("Delete/{id}")] 
        public async Task<IActionResult> Delete(int id) 
        { 
            var personal = await context.Personal.FindAsync(id); 
            if (personal == null) 
            { 
                return NotFound(new { error = "El personal no fue encontrado." }); 
            } 
            context.Personal.Remove(personal); 
            await context.SaveChangesAsync(); 
            return Ok(new { message = "Personal eliminado exitosamente." }); 
        }
    }
}
