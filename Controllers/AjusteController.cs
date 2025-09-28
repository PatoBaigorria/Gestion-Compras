using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Gestion_Compras.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class AjusteController : Controller
    {
        private readonly DataContext context;

        public AjusteController(DataContext context)
        {
            this.context = context;
        }

        // GET: Ajuste
        public async Task<IActionResult> Index()
        {
            var ajustes = await context.Ajuste
                .OrderByDescending(a => a.FechaAjuste)
                .ToListAsync();

            // Cargar manualmente los Items relacionados
            foreach (var ajuste in ajustes)
            {
                ajuste.Item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == ajuste.ItemCodigo);
            }

            return View(ajustes);
        }

        // GET: Ajuste/BuscarAjustes
        [HttpGet]
        public async Task<IActionResult> BuscarAjustes(string searchTerm = "")
        {
            var ajustes = await context.Ajuste
                .OrderByDescending(a => a.FechaAjuste)
                .ToListAsync();

            // Cargar manualmente los Items relacionados
            foreach (var ajuste in ajustes)
            {
                ajuste.Item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == ajuste.ItemCodigo);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                // Filtrar en el lado del cliente después de obtener los datos
                bool isNumeric = int.TryParse(searchTerm, out int numericValue);
                
                ajustes = ajustes.Where(a => 
                    (a.ItemCodigo?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true) ||
                    (a.Item?.Descripcion?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true) ||
                    (a.Observaciones?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true) ||
                    (isNumeric && (a.StockIni == numericValue || a.StockReal == numericValue)) ||
                    a.FechaAjuste.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            return PartialView("_AjustesTable", ajustes);
        }

        // GET: Ajuste/BuscarAjustesJson
        [HttpGet]
        [Route("Ajuste/BuscarAjustesJson")]
        public async Task<IActionResult> BuscarAjustesJson(string searchTerm = "", int pagina = 1, int tamanoPagina = 100)
        {
            pagina = Math.Max(1, pagina);
            tamanoPagina = tamanoPagina <= 0 ? 100 : tamanoPagina;

            var query = context.Ajuste
                .Join(context.Item, a => a.ItemCodigo, i => i.Codigo, (a, i) => new { a, i })
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower();
                bool isNumeric = int.TryParse(searchTerm, out int n);
                query = query.Where(x =>
                    (x.a.ItemCodigo ?? "").ToLower().Contains(term) ||
                    (x.i.Descripcion ?? "").ToLower().Contains(term) ||
                    (x.a.Observaciones ?? "").ToLower().Contains(term) ||
                    (isNumeric && (x.a.StockIni == n || x.a.StockReal == n)) ||
                    x.a.FechaAjuste.ToString().Contains(searchTerm)
                );
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.a.FechaAjuste)
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(x => new
                {
                    codigo = x.a.ItemCodigo,
                    descripcion = x.i.Descripcion,
                    stockIni = x.a.StockIni,
                    stockReal = x.a.StockReal,
                    observaciones = x.a.Observaciones ?? "",
                    fecha = x.a.FechaAjuste
                })
                .ToListAsync();

            return Ok(new { items, total, pagina, tamanoPagina });
        }

        // GET: Ajuste/ObtenerItemPorCodigo
        [HttpGet]
        public async Task<IActionResult> ObtenerItemPorCodigo(string codigo)
        {
            if (string.IsNullOrEmpty(codigo))
            {
                return Json(new { success = false, message = "Código no proporcionado" });
            }

            var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == codigo);
            
            if (item == null)
            {
                return Json(new { success = false, message = "Item no encontrado" });
            }

            return Json(new { 
                success = true, 
                item = new { 
                    id = item.Id, 
                    codigo = item.Codigo, 
                    descripcion = item.Descripcion, 
                    stock = item.Stock 
                } 
            });
        }

        // POST: Ajuste/GuardarAjuste
        [HttpPost]
        [Route("Ajuste/GuardarAjuste")]
        public async Task<IActionResult> GuardarAjuste([FromBody] dynamic ajusteData)
        {
            try
            {
                // Obtener usuario logueado (Id)
                int? usuarioId = null;
                if (User?.Identity?.IsAuthenticated == true)
                {
                    if (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid))
                        usuarioId = uid;
                }
                string itemCodigo = ajusteData.ItemCodigo;
                int stockIni = ajusteData.StockIni;
                int stockReal = ajusteData.StockReal;
                string observaciones = ajusteData.Observaciones ?? "";

                if (string.IsNullOrEmpty(itemCodigo))
                {
                    return Json(new { success = false, message = "Código de item requerido" });
                }

                // Buscar el item
                var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == itemCodigo);
                if (item == null)
                {
                    return Json(new { success = false, message = "Item no encontrado" });
                }

                // Crear el ajuste
                var ajuste = new Ajuste
                {
                    ItemCodigo = itemCodigo.ToUpper(),
                    StockIni = stockIni,
                    StockReal = stockReal,
                    Observaciones = observaciones,
                    FechaAjuste = DateOnly.FromDateTime(DateTime.Now)
                };

                // Guardar el ajuste
                context.Ajuste.Add(ajuste);

                // Crear registro en Kardex
                var kardex = new Kardex
                {
                    ItemId = item.Id,
                    StockIni = item.Stock,
                    Cantidad = stockReal - stockIni,
                    TipoDeMov = "Ajuste",
                    FechaRegistro = DateTime.Now,
                    FechaMov = DateOnly.FromDateTime(DateTime.Now),
                    UsuarioId = usuarioId
                };
                context.Kardex.Add(kardex);

                // Actualizar el stock del item
                item.Stock = stockReal;
                context.Item.Update(item);

                await context.SaveChangesAsync();

                return Json(new { success = true, message = "Ajuste guardado exitosamente" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al guardar: " + ex.Message });
            }
        }
    }
}