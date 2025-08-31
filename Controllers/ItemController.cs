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

            var unidadesMedida = await context.UnidadDeMedida
                .Select(um => new UnidadDeMedida { Id = um.Id, Abreviatura = um.Abreviatura })
                .ToListAsync();

            var modelo = new FamiliaSubFamiliaViewModel
            {
                FamiliaList = familias,
                UnidadDeMedidaList = unidadesMedida
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


        [HttpGet("GetItemById")]
        public async Task<IActionResult> GetItemById(int id)
        {
            try
            {
                Console.WriteLine($"Obteniendo ítem con ID: {id}");
                
                var item = await context.Item
                    .Include(i => i.SubFamilia)
                    .ThenInclude(sf => sf.Familia)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (item == null)
                {
                    Console.WriteLine($"No se encontró el ítem con ID: {id}");
                    return NotFound(new { success = false, message = $"No se encontró el ítem con ID: {id}" });
                }

                Console.WriteLine($"Datos del ítem {id}: UnidadDeMedidaId={item.UnidadDeMedidaId}");

                // Validar que la unidad de medida exista
                if (item.UnidadDeMedidaId > 0)
                {
                    var unidadExiste = await context.UnidadDeMedida.AnyAsync(um => um.Id == item.UnidadDeMedidaId);
                    if (!unidadExiste)
                    {
                        Console.WriteLine($"ADVERTENCIA: No existe la unidad de medida con ID: {item.UnidadDeMedidaId}");
                        // No fallamos, solo registramos la advertencia
                    }
                }

                var result = new 
                {
                    id = item.Id,
                    codigo = item.Codigo,
                    descripcionItem = item.Descripcion,
                    unidadDeMedidaId = item.UnidadDeMedidaId,
                    puntoDePedido = item.PuntoDePedido,
                    precio = item.Precio,
                    critico = item.Critico,
                    familiaId = item.SubFamilia?.FamiliaId ?? 0,
                    subFamiliaId = item.SubFamiliaId
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR en GetItemById: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("ActualizarItem")]
        public async Task<IActionResult> ActualizarItem([FromBody] ItemViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var item = await context.Item.FindAsync(modelo.Id);
                if (item == null)
                {
                    return NotFound(new { success = false, message = "Ítem no encontrado" });
                }

                // Validar que el código no esté duplicado (excepto para el mismo ítem)
                if (await context.Item.AnyAsync(i => i.Codigo == modelo.Codigo && i.Id != modelo.Id))
                {
                    return BadRequest(new { success = false, message = "Ya existe un ítem con este código" });
                }

                // Actualizar propiedades
                item.Codigo = modelo.Codigo;
                item.Descripcion = modelo.Descripcion;
                item.UnidadDeMedidaId = modelo.UnidadDeMedidaId;
                item.PuntoDePedido = modelo.PuntoDePedido;
                item.Precio = modelo.Precio;
                item.Critico = modelo.Critico;
                item.SubFamiliaId = modelo.SubFamiliaId;

                context.Update(item);
                await context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
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
