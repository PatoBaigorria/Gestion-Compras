using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;
using Gestion_Compras.ViewModels;
using System.Collections.Generic;

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
        public async Task<ActionResult<IEnumerable<object>>> BuscarItems(string codigo = null, [FromQuery] List<int> familiaIds = null, [FromQuery] List<int> subFamiliaIds = null, string descripcion = null)
        {
            var query = context.Item.Where(i => i.Activo).AsQueryable();

            if (!string.IsNullOrEmpty(codigo))
            {
                query = query.Where(i => i.Codigo == codigo);
            }

            if (familiaIds != null && familiaIds.Count > 0)
            {
                query = query.Where(i => familiaIds.Contains(i.SubFamilia.FamiliaId));
            }

            if (subFamiliaIds != null && subFamiliaIds.Count > 0)
            {
                query = query.Where(i => subFamiliaIds.Contains(i.SubFamiliaId));
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
                    CantidadEnPedidos = i.CantidadEnPedidos,
                    CantidadEnPedidosPendientes = context.Pedido
                        .Where(p => p.ItemCodigo == i.Codigo && p.Estado == "PENDIENTE")
                        .Sum(p => (int?)p.Cantidad) ?? 0
                })
                .ToListAsync();

            return Ok(items);
        }


        [HttpGet("Exportar")]
        public async Task<IActionResult> Exportar(string codigo = null, [FromQuery] List<int> familiaIds = null, [FromQuery] List<int> subFamiliaIds = null, string descripcion = null)
        {
            var query = context.Item.Where(i => i.Activo).AsQueryable();

            if (!string.IsNullOrEmpty(codigo))
            {
                query = query.Where(i => i.Codigo == codigo);
            }

            if (familiaIds != null && familiaIds.Count > 0)
            {
                query = query.Where(i => familiaIds.Contains(i.SubFamilia.FamiliaId));
            }

            if (subFamiliaIds != null && subFamiliaIds.Count > 0)
            {
                query = query.Where(i => subFamiliaIds.Contains(i.SubFamiliaId));
            }

            if (!string.IsNullOrEmpty(descripcion))
            {
                query = query.Where(i => i.Descripcion.Contains(descripcion));
            }

            var items = await query
                .Select(i => new
                {
                    i.Codigo,
                    FamiliaDescripcion = i.SubFamilia.Familia.Descripcion,
                    SubFamiliaDescripcion = i.SubFamilia.Descripcion,
                    DescripcionItem = i.Descripcion,
                    i.Stock,
                    i.PuntoDePedido,
                    CantidadEnPedidos = i.CantidadEnPedidos,
                    UnidadDeMedidaAbreviatura = i.UnidadDeMedida.Abreviatura,
                    i.Precio,
                    i.Critico
                })
                .ToListAsync();

            // Construir CSV con BOM para Excel
            var sb = new System.Text.StringBuilder();
            string[] headers = new[] {
                "Codigo","Familia","Subfamilia","Descripcion Items","Stock","Punto de Pedido","Cant. Pedidos","Unidad de Medida","Precio","Critico","Comprar"
            };
            sb.AppendLine(string.Join(';', headers));

            foreach (var it in items)
            {
                double stock = it.Stock;
                double pp = it.PuntoDePedido;
                int cantPed = it.CantidadEnPedidos;
                bool necesitaComprar = (stock < pp) && ((stock + cantPed) < pp);

                string CriticoStr = it.Critico ? "SI" : "NO";
                string ComprarStr = necesitaComprar ? "SI" : "NO";

                var cols = new List<string>
                {
                    EscaparCsv(it.Codigo),
                    EscaparCsv(it.FamiliaDescripcion),
                    EscaparCsv(it.SubFamiliaDescripcion),
                    EscaparCsv(it.DescripcionItem),
                    it.Stock.ToString(),
                    it.PuntoDePedido.ToString(),
                    it.CantidadEnPedidos.ToString(),
                    EscaparCsv(it.UnidadDeMedidaAbreviatura),
                    it.Precio.ToString(),
                    CriticoStr,
                    ComprarStr
                };

                sb.AppendLine(string.Join(';', cols));
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();
            var fileName = $"items_export_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        private static string EscaparCsv(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            bool requiere = input.Contains('"') || input.Contains(';') || input.Contains('\n') || input.Contains('\r');
            var texto = input.Replace("\"", "\"\"");
            return requiere ? $"\"{texto}\"" : texto;
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
