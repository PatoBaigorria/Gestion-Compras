using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Gestion_Compras.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Gestion_Compras.Controllers
{
    [Authorize(Roles = "Administrador,Pañolero")]
    public class DevolucionController : Controller
    {
        private readonly DataContext context;

        public DevolucionController(DataContext context)
        {
            this.context = context;
        }

        // GET: /Devolucion
        public IActionResult Index()
        {
            var devoluciones = context.Devolucion
                .Include(d => d.Item)
                .Include(d => d.Personal)
                .OrderByDescending(d => d.FechaDevolucion)
                .ToList();

            ViewBag.PersonalList = context.Personal
                .OrderBy(p => p.NombreYApellido)
                .ToList();

            return View("~/Views/Devolucion/Index.cshtml", devoluciones);
        }

        // GET: /Devolucion/Buscar
        [HttpGet]
        public IActionResult Buscar(string searchTerm = "")
        {
            var devoluciones = context.Devolucion
                .Include(d => d.Item)
                .Include(d => d.Personal)
                .OrderByDescending(d => d.FechaDevolucion)
                .ToList();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                bool isNumeric = int.TryParse(searchTerm, out int n);
                devoluciones = devoluciones.Where(d =>
                    (d.Item != null && (
                        (d.Item.Codigo ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        (d.Item.Descripcion ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                    )) ||
                    (d.Observaciones ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (d.Personal != null && (
                        (d.Personal.NombreYApellido ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                    )) ||
                    (isNumeric && d.Cantidad == n) ||
                    d.FechaDevolucion.ToString("dd/MM/yyyy").Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            return PartialView("~/Views/Devolucion/_DevolucionesTable.cshtml", devoluciones);
        }

        // GET: /Devolucion/BuscarJson
        [HttpGet]
        [Route("Devolucion/BuscarJson")]
        public async Task<IActionResult> BuscarJson(string searchTerm = "", int pagina = 1, int tamanoPagina = 100)
        {
            pagina = Math.Max(1, pagina);
            tamanoPagina = tamanoPagina <= 0 ? 100 : tamanoPagina;

            var query = context.Devolucion
                .Include(d => d.Item)
                .Include(d => d.Personal)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                bool isNumeric = int.TryParse(searchTerm, out int n);
                query = query.Where(d =>
                    (d.Item != null && (
                        (d.Item.Codigo ?? "").Contains(searchTerm) ||
                        (d.Item.Descripcion ?? "").Contains(searchTerm)
                    )) ||
                    (d.Observaciones ?? "").Contains(searchTerm) ||
                    (d.Personal != null && (
                        (d.Personal.NombreYApellido ?? "").Contains(searchTerm)
                    )) ||
                    (isNumeric && d.Cantidad == n) ||
                    d.FechaDevolucion.ToString("dd/MM/yyyy").Contains(searchTerm)
                );
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(d => d.FechaDevolucion)
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(d => new
                {
                    codigo = d.Item != null ? d.Item.Codigo : "",
                    descripcion = d.Item != null ? d.Item.Descripcion : "",
                    cantidad = d.Cantidad,
                    personal = d.Personal != null ? d.Personal.NombreYApellido : "",
                    observaciones = d.Observaciones ?? "",
                    fecha = d.FechaDevolucion.ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            return Ok(new { items, total, pagina, tamanoPagina });
        }

        // GET: /Devolucion/ObtenerItemPorCodigo
        [HttpGet]
        public async Task<IActionResult> ObtenerItemPorCodigo(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return Json(new { success = false, message = "Código no proporcionado" });
            }

            // Buscar todos los items con código similar (para debug, como en Pedido)
            var todosLosItems = await context.Item
                .Where(i => i.Codigo != null && i.Codigo.Contains(codigo.ToUpper()))
                .Select(i => new { i.Codigo, i.Activo, i.Descripcion })
                .ToListAsync();

            var item = await context.Item
                .Where(i => i.Codigo != null && i.Codigo == codigo.ToUpper())
                .FirstOrDefaultAsync();
            if (item == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Item no encontrado",
                    debug = new
                    {
                        codigoBuscado = codigo.ToUpper(),
                        itemsSimilares = todosLosItems
                    }
                });
            }

            if (!item.Activo)
            {
                return Json(new
                {
                    success = false,
                    message = $"El item {item.Codigo} está inactivo",
                    inactivo = true
                });
            }

            return Json(new
            {
                success = true,
                item = new
                {
                    id = item.Id,
                    codigo = item.Codigo,
                    descripcion = item.Descripcion,
                    stock = item.Stock
                }
            });
        }

        // POST: /Devolucion/Guardar
        [HttpPost]
        [Route("Devolucion/Guardar")] 
        public async Task<IActionResult> Guardar([FromBody] dynamic data)
        {
            try
            {
                string itemCodigo = data.ItemCodigo;
                double cantidad = data.Cantidad;
                int personalId = data.PersonalId;
                string observaciones = data.Observaciones ?? "";
                string? fechaStr = data.FechaDevolucion;

                if (string.IsNullOrWhiteSpace(itemCodigo))
                {
                    return Json(new { success = false, message = "Código de item requerido" });
                }
                if (cantidad <= 0)
                {
                    return Json(new { success = false, message = "La cantidad debe ser mayor a cero" });
                }

                // Normalizar el código para evitar problemas de casing o espacios
                itemCodigo = itemCodigo.Trim().ToUpper();

                var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo != null && i.Codigo == itemCodigo);
                if (item == null)
                {
                    return Json(new { success = false, message = "Item no encontrado" });
                }

                var personal = await context.Personal.FindAsync(personalId);
                if (personal == null)
                {
                    return Json(new { success = false, message = "Personal inválido" });
                }

                double stockAnterior = item.Stock;

                // Parsear fecha enviada desde el modal (input datetime-local => "yyyy-MM-ddTHH:mm")
                DateTime fechaDevolucion;
                if (!string.IsNullOrWhiteSpace(fechaStr) && DateTime.TryParse(fechaStr, out var parsed))
                {
                    fechaDevolucion = parsed;
                }
                else
                {
                    // Fallback: ahora
                    fechaDevolucion = DateTime.Now;
                }

                // Registrar devolución
                var devolucion = new Devolucion
                {
                    ItemId = item.Id,
                    PersonalId = personal.Id,
                    Cantidad = cantidad,
                    FechaDevolucion = fechaDevolucion,
                    Observaciones = observaciones
                };
                context.Devolucion.Add(devolucion);

                // Actualizar stock (sumar)
                item.Stock += cantidad;
                context.Item.Update(item);

                // Registrar en Kardex
                var kardex = new Kardex
                {
                    ItemId = item.Id,
                    StockIni = stockAnterior,
                    Cantidad = cantidad,
                    TipoDeMov = "Devolucion",
                    FechaRegistro = DateTime.Now,
                    FechaMov = DateOnly.FromDateTime(fechaDevolucion),
                    UsuarioId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null
                };
                context.Kardex.Add(kardex);

                var afectados = await context.SaveChangesAsync();
                Console.WriteLine($"[DEVOLUCION] Item {item.Codigo}: stockAnterior={stockAnterior}, cantidadDevuelta={cantidad}, stockNuevo={item.Stock}, filasAfectadas={afectados}");
                return Json(new { success = true, message = "Devolución registrada exitosamente", stockNuevo = item.Stock });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al guardar: " + ex.Message });
            }
        }
    }
}
