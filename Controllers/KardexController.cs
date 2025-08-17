using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gestion_Compras.Models;

namespace Gestion_Compras.Controllers
{
    public class KardexController : Controller
    {
        private readonly DataContext context;

        public KardexController(DataContext context)
        {
            this.context = context;
        }

        // GET: /Kardex/Index
        public async Task<IActionResult> Index()
        {
            ViewBag.UserName = User.Identity.Name;
            return View();
        }

        // GET: /Kardex/BuscarKardex
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> BuscarKardex(
            string codigo = null, 
            string descripcion = null, 
            DateTime? fechaDesde = null, 
            string tipoMovimiento = null,
            double? stockInicial = null,
            DateTime? fechaVale = null)
        {
            var query = context.Kardex
                .Include(k => k.Item)
                .AsQueryable();

            // Filtros
            if (!string.IsNullOrEmpty(codigo))
            {
                query = query.Where(k => k.Item.Codigo.Contains(codigo));
            }

            if (!string.IsNullOrEmpty(descripcion))
            {
                query = query.Where(k => k.Item.Descripcion.Contains(descripcion));
            }

            if (fechaDesde.HasValue)
            {
                query = query.Where(k => k.FechaRegistro >= fechaDesde.Value);
            }

            if (!string.IsNullOrEmpty(tipoMovimiento))
            {
                query = query.Where(k => k.TipoDeMov == tipoMovimiento);
            }

            if (stockInicial.HasValue)
            {
                query = query.Where(k => k.StockIni == stockInicial.Value);
            }

            if (fechaVale.HasValue)
            {
                var fechaValeOnly = DateOnly.FromDateTime(fechaVale.Value);
                query = query.Where(k => k.FechaMov == fechaValeOnly);
            }

            var movimientos = await query
                .Where(k => k.FechaRegistro != DateTime.MinValue) // Filtrar registros con fechas válidas
                .OrderBy(k => k.ItemId)
                .ThenBy(k => k.FechaRegistro)
                .Select(k => new
                {
                    id = k.Id,
                    fechaRegistro = k.FechaRegistro,
                    fechaVale = k.FechaMov,
                    codigo = k.Item.Codigo,
                    descripcion = k.Item.Descripcion,
                    tipoMovimiento = k.TipoDeMov,
                    stockInicial = k.StockIni,
                    cantidad = k.Cantidad,
                    itemId = k.ItemId
                })
                .ToListAsync();

            // Calcular stock final para cada movimiento
            var movimientosConStockFinal = new List<object>();
            var stockPorItem = new Dictionary<int, double>();

            foreach (var mov in movimientos)
            {
                // Inicializar stock si es la primera vez que vemos este item
                if (!stockPorItem.ContainsKey(mov.itemId))
                {
                    stockPorItem[mov.itemId] = mov.stockInicial;
                }

                // Calcular el cambio en stock según el tipo de movimiento
                double cambioStock = 0;
                switch (mov.tipoMovimiento?.ToLower())
                {
                    case "ingreso":
                        cambioStock = mov.cantidad;
                        break;
                    case "salida":
                        cambioStock = -mov.cantidad;
                        break;
                    case "ajuste":
                        cambioStock = mov.cantidad; // Puede ser positivo o negativo
                        break;
                }

                // Actualizar stock acumulado
                stockPorItem[mov.itemId] += cambioStock;

                movimientosConStockFinal.Add(new
                {
                    id = mov.id,
                    fechaRegistro = mov.fechaRegistro.ToString("dd/MM/yyyy HH:mm"),
                    fechaMovimiento = mov.fechaVale != DateOnly.MinValue ? mov.fechaVale.ToString("dd/MM/yyyy") : "",
                    codigo = mov.codigo,
                    descripcion = mov.descripcion,
                    tipoMovimiento = mov.tipoMovimiento,
                    stockInicial = mov.stockInicial,
                    cantidadMovimiento = mov.tipoMovimiento?.ToLower() == "salida" ? $"-{mov.cantidad}" : $"+{mov.cantidad}",
                    stockFinal = stockPorItem[mov.itemId]
                });
            }

            return Ok(movimientosConStockFinal);
        }
    }
}