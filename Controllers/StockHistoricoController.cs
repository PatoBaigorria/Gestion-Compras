using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gestion_Compras.Models;
using Microsoft.AspNetCore.Authorization;

namespace Gestion_Compras.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StockHistoricoController : ControllerBase
    {
        private readonly DataContext context;

        public StockHistoricoController(DataContext context)
        {
            this.context = context;
        }

        // GET: api/StockHistorico/ConsultarStock
        [HttpGet("ConsultarStock")]
        public async Task<IActionResult> ConsultarStock(
            DateTime? fecha = null,
            int? familiaId = null,
            int? subFamiliaId = null,
            string codigo = null,
            string descripcion = null)
        {
            try
            {
                // Validar que se proporcione una fecha
                if (!fecha.HasValue)
                {
                    return BadRequest(new { mensaje = "Debe proporcionar una fecha para consultar" });
                }

                // Obtener items según filtros
                var queryItems = context.Item
                    .Include(i => i.SubFamilia)
                        .ThenInclude(sf => sf.Familia)
                    .Include(i => i.UnidadDeMedida)
                    .Where(i => i.Activo)
                    .AsQueryable();

                if (familiaId.HasValue)
                {
                    queryItems = queryItems.Where(i => i.SubFamilia.FamiliaId == familiaId.Value);
                }

                if (subFamiliaId.HasValue)
                {
                    queryItems = queryItems.Where(i => i.SubFamiliaId == subFamiliaId.Value);
                }

                if (!string.IsNullOrEmpty(codigo))
                {
                    queryItems = queryItems.Where(i => i.Codigo.Contains(codigo));
                }

                if (!string.IsNullOrEmpty(descripcion))
                {
                    queryItems = queryItems.Where(i => i.Descripcion.Contains(descripcion));
                }

                var items = await queryItems.ToListAsync();

                if (!items.Any())
                {
                    return Ok(new { items = new List<object>(), mensaje = "No se encontraron items con los filtros especificados" });
                }

                var resultados = new List<object>();

                foreach (var item in items)
                {
                    // Obtener el último movimiento hasta la fecha especificada (o anterior más cercana)
                    var ultimoMovimiento = await context.Kardex
                        .Where(k => k.ItemId == item.Id && k.FechaRegistro <= fecha.Value.Date.AddDays(1).AddSeconds(-1))
                        .OrderByDescending(k => k.FechaRegistro)
                        .FirstOrDefaultAsync();

                    // Si no hay movimientos para este item, saltar
                    if (ultimoMovimiento == null)
                    {
                        continue;
                    }

                    // Calcular stock final del último movimiento
                    double stock = CalcularStockFinal(ultimoMovimiento);

                    resultados.Add(new
                    {
                        itemId = item.Id,
                        codigo = item.Codigo,
                        descripcion = item.Descripcion,
                        unidad = item.UnidadDeMedida?.Abreviatura ?? "",
                        stock = stock,
                        fechaMovimiento = ultimoMovimiento.FechaRegistro.ToString("dd/MM/yyyy HH:mm")
                    });
                }

                return Ok(new { items = resultados });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al consultar stock histórico", error = ex.Message });
            }
        }

        private double CalcularStockFinal(Kardex movimiento)
        {
            double stockFinal = 0;
            switch (movimiento.TipoDeMov?.ToLower())
            {
                case "ingreso":
                    stockFinal = movimiento.StockIni + movimiento.Cantidad;
                    break;
                case "salida":
                    stockFinal = movimiento.StockIni - movimiento.Cantidad;
                    break;
                case "ajuste":
                    stockFinal = movimiento.StockIni + movimiento.Cantidad;
                    break;
                case "devolucion":
                    stockFinal = movimiento.StockIni + movimiento.Cantidad;
                    break;
                default:
                    stockFinal = movimiento.StockIni;
                    break;
            }
            return stockFinal;
        }

        // GET: api/StockHistorico/ObtenerFamilias
        [HttpGet("ObtenerFamilias")]
        public async Task<IActionResult> ObtenerFamilias()
        {
            try
            {
                var familias = await context.Familia
                    .OrderBy(f => f.Descripcion)
                    .Select(f => new { id = f.Id, descripcion = f.Descripcion })
                    .ToListAsync();

                return Ok(familias);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al obtener familias", error = ex.Message });
            }
        }

        // GET: api/StockHistorico/ObtenerSubFamilias
        [HttpGet("ObtenerSubFamilias")]
        public async Task<IActionResult> ObtenerSubFamilias(int? familiaId = null)
        {
            try
            {
                var query = context.SubFamilia.AsQueryable();

                if (familiaId.HasValue)
                {
                    query = query.Where(sf => sf.FamiliaId == familiaId.Value);
                }

                var subFamilias = await query
                    .OrderBy(sf => sf.Descripcion)
                    .Select(sf => new { id = sf.Id, descripcion = sf.Descripcion, familiaId = sf.FamiliaId })
                    .ToListAsync();

                return Ok(subFamilias);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al obtener subfamilias", error = ex.Message });
            }
        }
    }
}
