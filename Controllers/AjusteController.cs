using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;

namespace Gestion_Compras.Controllers
{
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
                    FechaMov = DateOnly.FromDateTime(DateTime.Now)
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