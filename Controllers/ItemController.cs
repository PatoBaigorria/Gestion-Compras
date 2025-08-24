using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;
using Gestion_Compras.ViewModels;

namespace Gestion_Compras.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ItemController : Controller
    {
        private readonly DataContext context;

        public ItemController(DataContext context)
        {
            this.context = context;
        }


        // Acción para mostrar la vista del buscador de items
        [HttpGet("Buscador")]
        public async Task<IActionResult> Buscador()
        {
            var familias = await context.Familia
                .Select(f => new Familia { Id = f.Id, Codigo = f.Codigo, Descripcion = f.Descripcion })
                .ToListAsync();

            var modelo = new FamiliaSubFamiliaViewModel
            {
                FamiliaList = familias
            };

            return View(modelo);
        }



        [HttpGet("BuscarItems")]
        public async Task<ActionResult<IEnumerable<object>>> BuscarItems(string codigo = null, int? familiaId = null, int? subFamiliaId = null, string descripcion = null)
        {
            var query = context.Item.Where(i => i.Activo).AsQueryable();

            if (!string.IsNullOrEmpty(codigo))
            {
                query = query.Where(i => i.Codigo == codigo);
            }

            if (familiaId.HasValue)
            {
                query = query.Where(i => i.SubFamilia.FamiliaId == familiaId.Value);
            }

            if (subFamiliaId.HasValue)
            {
                query = query.Where(i => i.SubFamiliaId == subFamiliaId.Value);
            }

            if (!string.IsNullOrEmpty(descripcion))
            {
                query = query.Where(i => i.Descripcion.Contains(descripcion));
            }

            var items = await query
                .Select(i => new
                {
                    i.Id,
                    i.Codigo,
                    i.Descripcion,
                    i.Stock,
                    i.PuntoDePedido,
                    i.Precio,
                    i.Critico,
                    i.Activo,
                    FamiliaDescripcion = i.SubFamilia.Familia.Descripcion,
                    SubFamiliaDescripcion = i.SubFamilia.Descripcion,
                    UnidadDeMedidaAbreviatura = i.UnidadDeMedida.Abreviatura,
                    DescripcionItem = i.Descripcion,
                    CantidadEnPedidosPendientes = context.Pedido
                        .Where(p => p.ItemCodigo == i.Codigo && p.Estado == "PENDIENTE")
                        .Sum(p => (int?)p.Cantidad) ?? 0
                })
                .ToListAsync();

            return Ok(items);
        }


        [HttpPost("PostItem")]
        public async Task<ActionResult<Item>> PostItem(Item item)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var subFamilia = await context.SubFamilia
                .Where(sf => sf.Id == item.SubFamiliaId)
                .FirstOrDefaultAsync();

            if (subFamilia == null)
            {
                return BadRequest("Subfamilia no encontrada.");
            }

            var subFamiliaCodigo = subFamilia.Codigo;

            var items = await context.Item
                .Where(i => i.SubFamiliaId == item.SubFamiliaId)
                .ToListAsync();

            var maxCodigo = items
                .Select(i => int.TryParse(i.Codigo?.Substring(subFamiliaCodigo.Length), out var result) ? result : 0)
                .DefaultIfEmpty(0)
                .Max();

            var nuevoCodigo = $"{subFamiliaCodigo}{(maxCodigo + 1).ToString("D3")}";
            item.Codigo = nuevoCodigo;

            if (string.IsNullOrEmpty(item.Codigo))
            {
                return BadRequest("No se pudo generar el código del item.");
            }

            context.Item.Add(item);
            await context.SaveChangesAsync();

            // Devolver un mensaje de éxito
            return Ok(new { message = "Item creado con éxito", item });
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutItem(int id, Item item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }

            context.Entry(item).State = EntityState.Modified;

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ItemExists(id))
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
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await context.Item.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            context.Item.Remove(item);
            await context.SaveChangesAsync();

            return NoContent();
        }

        private bool ItemExists(int id)
        {
            return context.Item.Any(e => e.Id == id);
        }
    }
}
