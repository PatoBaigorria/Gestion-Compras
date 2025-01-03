using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;

namespace Gestion_Compras.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class FamiliaController : ControllerBase
    {
        private readonly DataContext context;

        public FamiliaController(DataContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Familia>>> GetFamilias()
        {
            return await context.Familia.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Familia>> GetFamilia(int id)
        {
            var familia = await context.Familia.FindAsync(id);

            if (familia == null)
            {
                return NotFound();
            }

            return familia;
        }

        [HttpPost]
        public async Task<ActionResult<Familia>> PostFamilia(Familia familia)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Verificar si ya existe una familia con el mismo código
            var familiaExistente = await context.Familia
                .Where(f => f.Codigo == familia.Codigo)
                .FirstOrDefaultAsync();

            if (familiaExistente != null)
            {
                return Conflict("Código de Familia Existente.");
            }

            context.Familia.Add(familia);
            await context.SaveChangesAsync();

            // Devolver un mensaje de éxito
            return Ok(new { message = "Familia creada exitosamente", familia });
        }



        [HttpPut("{id}")]
        public async Task<IActionResult> PutFamilia(int id, Familia familia)
        {
            if (id != familia.Id)
            {
                return BadRequest();
            }

            context.Entry(familia).State = EntityState.Modified;

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FamiliaExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFamilia(int id)
        {
            var familia = await context.Familia.FindAsync(id);
            if (familia == null)
            {
                return NotFound();
            }

            context.Familia.Remove(familia);
            await context.SaveChangesAsync();

            return NoContent();
        }

        private bool FamiliaExists(int id)
        {
            return context.Familia.Any(e => e.Id == id);
        }
    }
}
