using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;
using Gestion_Compras.ViewModels;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace Gestion_Compras.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class ItemController : Controller
    {
        private readonly DataContext context;

        // Para evitar problemas de concurrencia, usamos un objeto de lock
        private static readonly object _cacheLock = new object();
        private static List<ItemViewModel> _itemsCache = null;
        private static DateTime _lastCacheUpdate = DateTime.MinValue;

        public ItemController(DataContext context)
        {
            this.context = context;
        }

        // Añade esta clase dentro del namespace o en otro archivo
        public class ItemViewModel
        {
            public int Id { get; set; }
            public string Codigo { get; set; }
            public string Descripcion { get; set; }
            public double Stock { get; set; }
            public double PuntoDePedido { get; set; }
            public double Precio { get; set; }
            public bool Critico { get; set; }
            public bool Activo { get; set; }
            public string FamiliaDescripcion { get; set; }
            public string SubFamiliaDescripcion { get; set; }
            public string UnidadDeMedidaAbreviatura { get; set; }
            public double CantidadEnPedidos { get; set; }
            public int FamiliaId { get; set; }
            public int SubFamiliaId { get; set; }
            public int UnidadDeMedidaId { get; set; }
        }

        [HttpGet("ObtenerUnidadesDeMedida")]
        public async Task<IActionResult> ObtenerUnidadesDeMedida()
        {
            try
            {
                var unidades = await context.UnidadDeMedida
                    .Select(um => new { id = um.Id, descripcion = um.Abreviatura })
                    .OrderBy(um => um.descripcion)
                    .ToListAsync();

                return Ok(unidades);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener unidades de medida: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Error al obtener las unidades de medida" });
            }
        }

        [HttpGet("Buscador")]
        [Authorize]
        public async Task<IActionResult> Buscador()
        {
            // ⭐ DEBUGGING - Verificar autenticación ⭐
            Console.WriteLine($"=== ACCESO A BUSCADOR ===");
            Console.WriteLine($"Usuario autenticado: {User.Identity.IsAuthenticated}");
            Console.WriteLine($"Nombre de usuario: {User.Identity.Name}");
            Console.WriteLine($"Path: {HttpContext.Request.Path}");
            Console.WriteLine($"=== FIN DEBUG ===");

            if (!User.Identity.IsAuthenticated)
            {
                Console.WriteLine("🔐 USUARIO NO AUTENTICADO - Redirigiendo a login");
                return RedirectToAction("Login", "Autenticacion");
            }

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
        public async Task<ActionResult> BuscarItems(
    string codigo = null,
    [FromQuery] List<int> familiaIds = null,
    [FromQuery] List<int> subFamiliaIds = null,
    string descripcion = null,
    int pagina = 1,                    // ← NUEVO PARÁMETRO
    int tamanoPagina = 100)            // ← NUEVO PARÁMETRO
        {
            try
            {
                // Cargar en cache si está vacío o pasaron más de 5 minutos
                if (_itemsCache == null || (DateTime.Now - _lastCacheUpdate).TotalMinutes > 5)
                {
                    lock (_cacheLock)
                    {
                        if (_itemsCache == null || (DateTime.Now - _lastCacheUpdate).TotalMinutes > 5)
                        {
                            _itemsCache = context.Item
                                .Select(i => new ItemViewModel
                                {
                                    Id = i.Id,
                                    Codigo = i.Codigo,
                                    Descripcion = i.Descripcion,
                                    Stock = i.Stock,
                                    PuntoDePedido = i.PuntoDePedido,
                                    Precio = i.Precio,
                                    Critico = i.Critico,
                                    Activo = i.Activo,
                                    FamiliaDescripcion = i.SubFamilia.Familia.Descripcion,
                                    SubFamiliaDescripcion = i.SubFamilia.Descripcion,
                                    UnidadDeMedidaAbreviatura = i.UnidadDeMedida.Abreviatura,
                                    CantidadEnPedidos = i.CantidadEnPedidos,
                                    FamiliaId = i.SubFamilia.FamiliaId,
                                    SubFamiliaId = i.SubFamiliaId
                                })
                                .ToList();
                            _lastCacheUpdate = DateTime.Now;
                        }
                    }
                }

                // Aplicar filtros en memoria
                var query = _itemsCache.AsEnumerable();

                if (!string.IsNullOrEmpty(codigo))
                {
                    query = query.Where(i => i.Codigo.Contains(codigo, StringComparison.OrdinalIgnoreCase));
                }

                if (familiaIds != null && familiaIds.Count > 0)
                {
                    query = query.Where(i => familiaIds.Contains(i.FamiliaId));
                }

                if (subFamiliaIds != null && subFamiliaIds.Count > 0)
                {
                    query = query.Where(i => subFamiliaIds.Contains(i.SubFamiliaId));
                }

                if (!string.IsNullOrEmpty(descripcion))
                {
                    query = query.Where(i => i.Descripcion.Contains(descripcion, StringComparison.OrdinalIgnoreCase));
                }

                var itemsFiltrados = query.ToList();
                var totalItems = itemsFiltrados.Count;

                // ← PAGINACIÓN - ESTO ES NUEVO
                var itemsPagina = itemsFiltrados
                    .Skip((pagina - 1) * tamanoPagina)
                    .Take(tamanoPagina)
                    .ToList();

                return Ok(new
                {
                    items = itemsPagina,           // ← Solo 100 items en vez de todos
                    total = totalItems,
                    pagina,
                    totalPaginas = (int)Math.Ceiling(totalItems / (double)tamanoPagina),
                    tamanoPagina
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en BuscarItems: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Error al buscar items" });
            }
        }

        [HttpPost("RefreshCache")]
        public async Task<IActionResult> RefreshCache(bool immediateReload = false)
        {
            try
            {
                lock (_cacheLock)
                {
                    _itemsCache = null;
                    _lastCacheUpdate = DateTime.MinValue;
                }

                if (immediateReload)
                {
                    var reloadedItems = await CargarItemsEnMemoria();
                    lock (_cacheLock)
                    {
                        _itemsCache = reloadedItems;
                        _lastCacheUpdate = DateTime.Now;
                    }

                    return Ok(new
                    {
                        success = true,
                        message = "Cache recargado inmediatamente",
                        count = _itemsCache?.Count
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Cache invalidado, se recargará automáticamente en la próxima búsqueda"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error al refrescar cache: {ex.Message}"
                });
            }
        }

        private async Task<List<ItemViewModel>> CargarItemsEnMemoria()
        {
            return await context.Item
                .Select(i => new ItemViewModel
                {
                    Id = i.Id,
                    Codigo = i.Codigo,
                    Descripcion = i.Descripcion,
                    Stock = i.Stock,
                    PuntoDePedido = i.PuntoDePedido,
                    Precio = i.Precio,
                    Critico = i.Critico,
                    Activo = i.Activo,
                    FamiliaDescripcion = i.SubFamilia.Familia.Descripcion,
                    SubFamiliaDescripcion = i.SubFamilia.Descripcion,
                    UnidadDeMedidaAbreviatura = i.UnidadDeMedida.Abreviatura,
                    CantidadEnPedidos = i.CantidadEnPedidos,
                    FamiliaId = i.SubFamilia.FamiliaId,
                    SubFamiliaId = i.SubFamiliaId
                })
                .ToListAsync();
        }

        [HttpGet("Exportar")]
        public async Task<IActionResult> Exportar(
            string codigo = null, 
            [FromQuery] List<int> familiaIds = null, 
            [FromQuery] List<int> subFamiliaIds = null, 
            string descripcion = null,
            bool? comprar = null,
            bool? critico = null,
            bool? activo = null)
        {
            // Usar el cache para exportar tambin
            if (_itemsCache == null || (DateTime.Now - _lastCacheUpdate).TotalMinutes > 5)
            {
                await RefreshCache(true);
            }

            var query = _itemsCache.AsEnumerable();

            if (!string.IsNullOrEmpty(codigo))
            {
                query = query.Where(i => i.Codigo.Contains(codigo, StringComparison.OrdinalIgnoreCase));
            }

            if (familiaIds != null && familiaIds.Count > 0)
            {
                query = query.Where(i => familiaIds.Contains(i.FamiliaId));
            }

            if (subFamiliaIds != null && subFamiliaIds.Count > 0)
            {
                query = query.Where(i => subFamiliaIds.Contains(i.SubFamiliaId));
            }

            if (!string.IsNullOrEmpty(descripcion))
            {
                query = query.Where(i => i.Descripcion.Contains(descripcion, StringComparison.OrdinalIgnoreCase));
            }

            // Filtrar por activo
            if (activo.HasValue)
            {
                query = query.Where(i => i.Activo == activo.Value);
            }

            // Filtrar por crtico
            if (critico.HasValue && critico.Value)
            {
                query = query.Where(i => i.Critico);
            }

            // Filtrar por comprar (items que necesitan ser comprados)
            if (comprar.HasValue && comprar.Value)
            {
                query = query.Where(i => {
                    double stock = i.Stock;
                    double pp = i.PuntoDePedido;
                    double cantPed = i.CantidadEnPedidos;
                    return (stock < pp) && ((stock + cantPed) < pp);
                });
            }

            var items = query.ToList();

            // Construir CSV
            var sb = new System.Text.StringBuilder();
            string[] headers = new[] {
                "Codigo","Familia","Subfamilia","Descripcion Items","Stock","Punto de Pedido","Cant. Pedidos","Unidad de Medida","Precio","Critico","Comprar"
            };
            sb.AppendLine(string.Join(';', headers));

            foreach (var it in items)
            {
                double stock = it.Stock;
                double pp = it.PuntoDePedido;
                double cantPed = it.CantidadEnPedidos;
                bool necesitaComprar = (stock < pp) && ((stock + cantPed) < pp);

                string CriticoStr = it.Critico ? "SI" : "NO";
                string ComprarStr = necesitaComprar ? "SI" : "NO";

                var cols = new List<string>
                {
                    EscaparCsv(it.Codigo),
                    EscaparCsv(it.FamiliaDescripcion),
                    EscaparCsv(it.SubFamiliaDescripcion),
                    EscaparCsv(it.Descripcion),
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

        private static string EscaparCsv(string input)
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
                var item = await context.Item
                    .Include(i => i.SubFamilia)
                    .ThenInclude(sf => sf.Familia)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (item == null)
                {
                    return NotFound(new { success = false, message = $"No se encontró el ítem con ID: {id}" });
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
                    activo = item.Activo,
                    familiaId = item.SubFamilia?.FamiliaId ?? 0,
                    subFamiliaId = item.SubFamiliaId
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
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

                if (await context.Item.AnyAsync(i => i.Codigo == modelo.Codigo && i.Id != modelo.Id))
                {
                    return BadRequest(new { success = false, message = "Ya existe un ítem con este código" });
                }

                item.Codigo = modelo.Codigo;
                item.Descripcion = modelo.Descripcion;
                item.UnidadDeMedidaId = modelo.UnidadDeMedidaId; // ← CORREGIDO

                // CORREGIR LOS OPERADORES ?? - Eliminarlos ya que double no puede ser null
                item.PuntoDePedido = modelo.PuntoDePedido; // ← QUITAR ??
                item.Precio = modelo.Precio; // ← QUITAR ??

                item.Critico = modelo.Critico;
                item.SubFamiliaId = modelo.SubFamiliaId;
                item.Activo = modelo.Activo;

                context.Update(item);
                await context.SaveChangesAsync();

                // INVALIDAR CACHE después de modificar
                lock (_cacheLock)
                {
                    _itemsCache = null;
                    _lastCacheUpdate = DateTime.MinValue;
                }

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

            // INVALIDAR CACHE después de crear
            lock (_cacheLock)
            {
                _itemsCache = null;
                _lastCacheUpdate = DateTime.MinValue;
            }

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

                // INVALIDAR CACHE después de modificar
                lock (_cacheLock)
                {
                    _itemsCache = null;
                    _lastCacheUpdate = DateTime.MinValue;
                }
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

            // INVALIDAR CACHE después de eliminar
            lock (_cacheLock)
            {
                _itemsCache = null;
                _lastCacheUpdate = DateTime.MinValue;
            }

            return NoContent();
        }

        private bool ItemExists(int id)
        {
            return context.Item.Any(e => e.Id == id);
        }
    }
}