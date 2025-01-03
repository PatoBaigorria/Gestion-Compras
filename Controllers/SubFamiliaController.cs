using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gestion_Compras.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class SubFamiliaController : ControllerBase
    {
        private readonly DataContext context;

        public SubFamiliaController(DataContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SubFamilia>>> GetSubFamilias()
        {
            return await context.SubFamilia.Include(sf => sf.Familia).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SubFamilia>> GetSubFamilia(int id)
        {
            var subFamilia = await context.SubFamilia.Include(sf => sf.Familia).FirstOrDefaultAsync(sf => sf.Id == id);

            if (subFamilia == null)
            {
                return NotFound();
            }

            return subFamilia;
        }

        [HttpPost]
        public async Task<ActionResult<SubFamilia>> PostSubFamilia(SubFamilia subFamilia)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var codigoFamilia = await context.Familia
                .Where(f => f.Id == subFamilia.FamiliaId)
                .Select(f => f.Codigo)
                .FirstOrDefaultAsync();

            if (codigoFamilia == null)
            {
                return BadRequest("Familia no encontrada.");
            }

            // Obtener el máximo código de subfamilia existente en memoria
            var subFamilias = await context.SubFamilia
                .Where(sf => sf.FamiliaId == subFamilia.FamiliaId)
                .ToListAsync();

            var maxCodigo = subFamilias
                .Select(sf => int.TryParse(sf.Codigo.Substring(codigoFamilia.Length), out var result) ? result : 0)
                .DefaultIfEmpty(0)
                .Max();

            var nuevoCodigo = $"{codigoFamilia}{(maxCodigo + 1).ToString("D2")}";
            subFamilia.Codigo = nuevoCodigo;

            context.SubFamilia.Add(subFamilia);
            await context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSubFamilia), new { id = subFamilia.Id }, subFamilia);
        }









        [HttpPut("{id}")]
        public async Task<IActionResult> PutSubFamilia(int id, SubFamilia subFamilia)
        {
            if (id != subFamilia.Id)
            {
                return BadRequest();
            }

            context.Entry(subFamilia).State = EntityState.Modified;

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SubFamiliaExists(id))
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
        public async Task<IActionResult> DeleteSubFamilia(int id)
        {
            var subFamilia = await context.SubFamilia.FindAsync(id);
            if (subFamilia == null)
            {
                return NotFound();
            }

            context.SubFamilia.Remove(subFamilia);
            await context.SaveChangesAsync();

            return NoContent();
        }

        private bool SubFamiliaExists(int id)
        {
            return context.SubFamilia.Any(e => e.Id == id);
        }
    }
}
