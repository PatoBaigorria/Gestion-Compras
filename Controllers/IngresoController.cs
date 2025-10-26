using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;

namespace Gestion_Compras.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class IngresoController : Controller
    {
        private readonly DataContext context;

        public IngresoController(DataContext context)
        {
            this.context = context;
        }

        // Acción para mostrar la lista de ingresos 
        [HttpGet("Index")] 
        public IActionResult Index() 
        { 
            var ingresos = context.Ingreso.Include(i => i.Proveedor)
                                          .Include(i => i.Item)
                                          .Select(i => new
                                          {
                                              i.Id,
                                              i.ItemCodigo,
                                              ItemDescripcion = i.Item != null ? i.Item.Descripcion : "",
                                              i.CantidadIngreso,
                                              i.Proveedor,
                                              i.Remito,
                                              i.OrdenCompra,
                                              i.PedidoId,
                                              i.FechaRemito,
                                              NumeroPedido = context.Pedido.Where(p => p.Id == i.PedidoId).Select(p => p.NumeroPedido).FirstOrDefault()
                                          })
                                          .OrderByDescending(i => i.Id)
                                          .ToList(); 
            return View("~/Views/MaterialesIngreso/Index.cshtml", ingresos); 
        }

        // GET: /Ingreso/ListJson
        [HttpGet("ListJson")]
        public async Task<IActionResult> ListJson(string searchTerm = "", int pagina = 1, int tamanoPagina = 100)
        {
            pagina = Math.Max(1, pagina);
            tamanoPagina = tamanoPagina <= 0 ? 100 : tamanoPagina;

            var query = context.Ingreso
                .Include(i => i.Proveedor)
                .Include(i => i.Item)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower();
                var isNumeric = int.TryParse(searchTerm, out var numeroBusqueda);
                query = query.Where(i =>
                    (i.ItemCodigo ?? "").ToLower().Contains(term) ||
                    (i.Item != null && (i.Item.Descripcion ?? "").ToLower().Contains(term)) ||
                    (i.Proveedor != null && (i.Proveedor.RazonSocial ?? "").ToLower().Contains(term)) ||
                    ((i.Remito ?? "").ToLower().Contains(term)) ||
                    (isNumeric && i.OrdenCompra == numeroBusqueda)
                );
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(i => i.Id)
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(i => new
                {
                    id = i.Id,
                    itemCodigo = i.ItemCodigo,
                    itemDescripcion = i.Item != null ? i.Item.Descripcion : "",
                    cantidadIngreso = i.CantidadIngreso,
                    proveedor = i.Proveedor != null ? i.Proveedor.RazonSocial : "",
                    remito = i.Remito,
                    ordenCompra = i.OrdenCompra,
                    numeroPedido = context.Pedido.Where(p => p.Id == i.PedidoId).Select(p => p.NumeroPedido).FirstOrDefault(),
                    fechaRemito = i.FechaRemito.ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            return Ok(new { items, total, pagina, tamanoPagina });
        }

        // GET: /Ingreso/AltaIngresos
        [HttpGet("AltaIngresos")]
        public IActionResult AltaIngresos()
        {
            var personalList = context.Personal.ToList();
            var proveedorList = context.Proveedor.ToList();
            var itemList = context.Item.ToList();
            ViewBag.PersonalList = personalList;
            ViewBag.ItemList = itemList;
            ViewBag.ProveedorList = proveedorList;
            return View("~/Views/MaterialesIngreso/AltaIngresos.cshtml");
        }

        // GET: /Ingreso/ValidarItemEnPedido
        [HttpGet("ValidarItemEnPedido")]
        public async Task<IActionResult> ValidarItemEnPedido(string itemCodigo, int numeroPedido, double cantidadIngreso)
        {
            try
            {
                itemCodigo = itemCodigo?.ToUpper();
                // Buscar el pedido por número
                var pedido = await context.Pedido
                    .Where(p => p.NumeroPedido == numeroPedido)
                    .FirstOrDefaultAsync();

                if (pedido == null)
                {
                    return Ok(new { 
                        esValido = false, 
                        mensaje = $"No existe la relación entre el pedido {numeroPedido} y el item {itemCodigo}.",
                        cantidadDisponible = 0
                    });
                }

                // Buscar el item en ese pedido específico
                var itemEnPedido = await context.Pedido
                    .Where(p => p.NumeroPedido == numeroPedido && p.ItemCodigo == itemCodigo)
                    .FirstOrDefaultAsync();

                if (itemEnPedido == null)
                {
                    return Ok(new { 
                        esValido = false, 
                        mensaje = $"El item {itemCodigo} no pertenece al pedido número {numeroPedido}.",
                        cantidadDisponible = 0
                    });
                }

                // Calcular cuánto ya se ha recibido de este item en este pedido (columna Recibido)
                var cantidadYaIngresada = itemEnPedido.Recibido;

                var cantidadDisponible = itemEnPedido.Cantidad - cantidadYaIngresada;
                var cantidadTotalDespuesDeIngreso = cantidadYaIngresada + cantidadIngreso;

                // Debug: Log de valores para verificar cálculos
                Console.WriteLine($"DEBUG - Item: {itemCodigo}, Pedido: {numeroPedido}");
                Console.WriteLine($"DEBUG - Cantidad del pedido: {itemEnPedido.Cantidad}");
                Console.WriteLine($"DEBUG - Ya ingresada: {cantidadYaIngresada}");
                Console.WriteLine($"DEBUG - Intentando ingresar: {cantidadIngreso}");
                Console.WriteLine($"DEBUG - Total después: {cantidadTotalDespuesDeIngreso}");
                Console.WriteLine($"DEBUG - Disponible: {cantidadDisponible}");

                // Validar que la cantidad total (ya ingresada + nueva) no exceda la cantidad original del pedido
                if (cantidadTotalDespuesDeIngreso > itemEnPedido.Cantidad)
                {
                    return Ok(new { 
                        esValido = false, 
                        mensaje = $"ERROR: No se puede ingresar {cantidadIngreso} unidades del item {itemCodigo}.\n\n" +
                                 $"• Cantidad del pedido: {itemEnPedido.Cantidad}\n" +
                                 $"• Ya recibido: {cantidadYaIngresada}\n",
                        cantidadDisponible = cantidadDisponible
                    });
                }

                return Ok(new { 
                    esValido = true, 
                    mensaje = "Validación exitosa.",
                    cantidadDisponible = cantidadDisponible
                });
            }
            catch (Exception ex)
            {
                return Ok(new { 
                    esValido = false, 
                    mensaje = $"Error al validar: {ex.Message}",
                    cantidadDisponible = 0
                });
            }
        }

        // POST: /Ingreso/Create
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] List<Ingreso> ingresos)
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
                foreach (var ingreso in ingresos)
                {
                    ingreso.ItemCodigo = ingreso.ItemCodigo?.ToUpper();
                    Console.WriteLine($"Validando ingreso - ItemCodigo: {ingreso.ItemCodigo}, PedidoId: {ingreso.PedidoId}");
                    
                    // Verificar que el ítem existe
                    var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == ingreso.ItemCodigo);
                    if (item == null)
                    {
                        return BadRequest(new { error = $"El ítem con código {ingreso.ItemCodigo} no fue encontrado." });
                    }

                    Console.WriteLine($"Item encontrado - Id: {item.Id}, Codigo: {item.Codigo}");

                    // VALIDACIÓN CRÍTICA: Verificar que el ítem pertenece al pedido especificado
                    var pedido = await context.Pedido
                        .FirstOrDefaultAsync(p => p.NumeroPedido == ingreso.PedidoId && 
                                                 p.ItemCodigo == ingreso.ItemCodigo && 
                                                 p.Estado == "PENDIENTE");

                    if (pedido == null)
                    {
                        Console.WriteLine($"ERROR: El ítem {ingreso.ItemCodigo} no pertenece al pedido {ingreso.PedidoId} o el pedido no existe/está completado");
                        return BadRequest(new { error = $"El ítem {ingreso.ItemCodigo} no pertenece al pedido {ingreso.PedidoId} o el pedido no existe/está completado." });
                    }

                    Console.WriteLine($"Pedido encontrado - Id: {pedido.Id}, NumeroPedido: {pedido.NumeroPedido}");
                    
                    // Guardar el stock anterior para el Kardex
                    double stockAnterior = item.Stock;
                    
                    // Actualizar PedidoId con el Id real del pedido
                    ingreso.PedidoId = pedido.Id;
                    
                    // Asignar ItemId para la foreign key
                    ingreso.ItemId = item.Id;
                    
                    // Actualizar cantidad recibida en el pedido
                    pedido.Recibido += ingreso.CantidadIngreso;
                    
                    // Disminuir CantidadEnPedidos por la cantidad ingresada
                    item.CantidadEnPedidos -= (double)ingreso.CantidadIngreso;
                    if (item.CantidadEnPedidos < 0) item.CantidadEnPedidos = 0;
                    
                    Console.WriteLine($"CantidadEnPedidos actualizada: {item.CantidadEnPedidos} (disminuyó en {ingreso.CantidadIngreso})");
                    
                    // Verificar si el pedido está completo
                    if (pedido.Recibido >= pedido.Cantidad)
                    {
                        pedido.Estado = "COMPLETADO";
                        Console.WriteLine($"Pedido completado - Estado: {pedido.Estado}");
                    }
                    
                    // Actualizar stock del ítem
                    item.Stock += ingreso.CantidadIngreso;
                    
                    // Crear registro de Kardex con propiedades correctas
                    var kardexRegistro = new Kardex
                    {
                        ItemId = item.Id,
                        TipoDeMov = "INGRESO", // Usar TipoDeMov en lugar de TipoMov
                        Cantidad = ingreso.CantidadIngreso, // Usar Cantidad en lugar de CantMov
                        StockIni = stockAnterior, // Usar StockIni en lugar de StockAnterior
                        FechaMov = ingreso.FechaRemito,
                        FechaRegistro = DateTime.Now,
                        UsuarioId = usuarioId
                    };

                    // Actualizar entidades
                    context.Pedido.Update(pedido);
                    context.Item.Update(item);
                    context.Ingreso.Add(ingreso);
                    context.Kardex.Add(kardexRegistro);
                    
                    Console.WriteLine($"=== ANTES DE GUARDAR ===");
                    Console.WriteLine($"Item - Id: {item.Id}, Codigo: {item.Codigo}, Stock: {item.Stock}, CantidadEnPedidos: {item.CantidadEnPedidos}");
                    Console.WriteLine($"Pedido - Id: {pedido.Id}, NumeroPedido: {pedido.NumeroPedido}, Recibido: {pedido.Recibido}, Estado: {pedido.Estado}");
                    Console.WriteLine($"Ingreso - ItemCodigo: {ingreso.ItemCodigo}, Cantidad: {ingreso.CantidadIngreso}, PedidoId: {ingreso.PedidoId}");
                }

                await context.SaveChangesAsync();
                Console.WriteLine($"=== DESPUÉS DE GUARDAR ===");
                Console.WriteLine($"Cambios guardados exitosamente en la base de datos");
                return Ok(new { message = "Ingresos registrados exitosamente." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear ingreso: {ex.Message}");
                Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Error interno del servidor: " + ex.Message + (ex.InnerException != null ? " Inner: " + ex.InnerException.Message : "") });
            }
        }
    }
}
