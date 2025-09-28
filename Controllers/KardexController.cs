using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gestion_Compras.Models;
using System.Linq;

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
        public async Task<ActionResult> BuscarKardex(
            string codigo = null, 
            string descripcion = null, 
            DateTime? fechaDesde = null, 
            string tipoMovimiento = null,
            double? stockInicial = null,
            DateTime? fechaVale = null,
            int pagina = 1,
            int tamanoPagina = 100)
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

            // Conteo total (sin paginar) tras aplicar filtros base
            var totalFiltrado = await query.CountAsync();

            var movimientos = await query
                .Where(k => k.FechaRegistro != DateTime.MinValue) // Filtrar registros con fechas válidas
                .OrderBy(k => k.FechaRegistro) // Ordenar ascendente para calcular correctamente
                .ThenBy(k => k.FechaMov)
                .GroupJoin(context.Usuario, k => k.UsuarioId, u => u.Id, (k, usuarios) => new { k, usuario = usuarios.FirstOrDefault() })
                .Select(x => new
                {
                    id = x.k.Id,
                    fechaRegistro = x.k.FechaRegistro,
                    fechaVale = x.k.FechaMov,
                    codigo = x.k.Item.Codigo,
                    descripcion = x.k.Item.Descripcion,
                    tipoMovimiento = x.k.TipoDeMov,
                    stockInicial = x.k.StockIni,
                    cantidad = x.k.Cantidad,
                    itemId = x.k.ItemId,
                    usuario = x.usuario != null ? (x.usuario.Apellido + " " + x.usuario.Nombre) : ""
                })
                .ToListAsync();

            // Calcular stock final para cada movimiento
            var movimientosConStockFinal = new List<object>();
            var stockPorItem = new Dictionary<int, double>();

            foreach (var mov in movimientos)
            {
                // Para cada movimiento, el stock final es: stock inicial + cambio
                double stockFinal = 0;
                switch (mov.tipoMovimiento?.ToLower())
                {
                    case "ingreso":
                        stockFinal = mov.stockInicial + mov.cantidad;
                        break;
                    case "salida":
                        stockFinal = mov.stockInicial - mov.cantidad;
                        break;
                    case "ajuste":
                        // Para ajustes, el stock final es directamente la cantidad ajustada
                        stockFinal = mov.stockInicial + mov.cantidad;
                        break;
                    case "devolucion":
                        stockFinal = mov.stockInicial + mov.cantidad;
                        break;
                }

                // Determinar cómo mostrar la cantidad según el tipo de movimiento
                string cantidadMostrar;

                if (mov.tipoMovimiento?.ToLower() == "ajuste")
                {
                    // Para ajustes, mostrar la cantidad ajustada (stock final)
                    cantidadMostrar = stockFinal.ToString();
                }
                else
                {
                    // Para ingresos y salidas, mostrar la diferencia como antes
                    cantidadMostrar = mov.tipoMovimiento?.ToLower() == "salida" ? $"-{mov.cantidad}" : $"+{mov.cantidad}";
                }

                movimientosConStockFinal.Add(new
                {
                    id = mov.id,
                    fechaRegistro = mov.fechaRegistro.ToString("dd/MM/yyyy HH:mm"),
                    fechaMovimiento = mov.fechaVale != DateOnly.MinValue ? mov.fechaVale.ToString("dd/MM/yyyy") : "",
                    codigo = mov.codigo,
                    descripcion = mov.descripcion,
                    tipoMovimiento = mov.tipoMovimiento,
                    stockInicial = mov.stockInicial,
                    cantidadMovimiento = cantidadMostrar,
                    stockFinal = stockFinal,
                    usuario = mov.usuario
                });
            }

            // Ordenar los resultados por fecha descendente para mostrar los más recientes primero
            var movimientosOrdenados = movimientosConStockFinal
                .OrderByDescending(m => DateTime.Parse(((dynamic)m).fechaRegistro))
                .ToList();

            // Aplicar paginación en memoria después de calcular cantidades
            pagina = Math.Max(1, pagina);
            tamanoPagina = tamanoPagina <= 0 ? 100 : tamanoPagina;
            var itemsPaginados = movimientosOrdenados
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .ToList();

            return Ok(new {
                items = itemsPaginados,
                total = movimientosOrdenados.Count, // total tras cálculo (puede diferir de totalFiltrado si se filtrara aquí)
                pagina,
                tamanoPagina
            });
        }
    }
}