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
                    // Buscar en qué otros pedidos pendientes está este item
                    var pedidosConEsteItem = await context.Pedido
                        .Where(p => p.ItemCodigo == itemCodigo && 
                                   p.Estado == "PENDIENTE" &&
                                   p.Recibido < p.Cantidad) // Solo pedidos con cantidad pendiente
                        .Select(p => new {
                            p.NumeroPedido,
                            p.Cantidad,
                            p.Recibido,
                            Pendiente = p.Cantidad - p.Recibido
                        })
                        .ToListAsync();

                    string mensajeError = $"ERROR: El item {itemCodigo} no pertenece al pedido N°{numeroPedido}.";

                    // Si el item existe en otros pedidos, mostrarlos
                    if (pedidosConEsteItem.Any())
                    {
                        mensajeError += $"\n\n✓ Este item SÍ está en los siguientes pedidos pendientes:";
                        foreach (var pedidoConItem in pedidosConEsteItem)
                        {
                            mensajeError += $"\n• Pedido N°{pedidoConItem.NumeroPedido}:\n" +
                                          $"  - Cantidad pendiente: {pedidoConItem.Pendiente}";
                        }
                    }
                    else
                    {
                        mensajeError += $"\n\n⚠️ No hay pedidos pendientes con el item {itemCodigo}.";
                    }

                    return Ok(new { 
                        esValido = false, 
                        mensaje = mensajeError,
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
                    // Buscar otros pedidos pendientes con el mismo item
                    var otrosPedidosPendientes = await context.Pedido
                        .Where(p => p.ItemCodigo == itemCodigo && 
                                   p.Estado == "PENDIENTE" && 
                                   p.NumeroPedido != numeroPedido &&
                                   p.Recibido < p.Cantidad) // Solo pedidos con cantidad pendiente
                        .Select(p => new {
                            p.NumeroPedido,
                            p.Cantidad,
                            p.Recibido,
                            Pendiente = p.Cantidad - p.Recibido
                        })
                        .ToListAsync();

                    string mensajeError = $"ERROR: No se puede ingresar {cantidadIngreso} unidades del item {itemCodigo}.\n\n" +
                                         $"• Pedido N°{numeroPedido}:\n" +
                                         $"  - Cantidad pendiente: {cantidadDisponible}";

                    // Si hay otros pedidos pendientes con el mismo item, informarlos
                    if (otrosPedidosPendientes.Any())
                    {
                        mensajeError += "\n\nOTROS PEDIDOS PENDIENTES CON ESTE ITEM:";
                        foreach (var otroPedido in otrosPedidosPendientes)
                        {
                            mensajeError += $"\n• Pedido N°{otroPedido.NumeroPedido}:\n" +
                                          $"  - Cantidad pendiente: {otroPedido.Pendiente}";
                        }
                    }

                    return Ok(new { 
                        esValido = false, 
                        mensaje = mensajeError,
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

        // DTO para recibir ingresos con precio (el precio no se guarda en Ingreso, solo actualiza Item)
        public class IngresoConPrecioDto
        {
            public string ItemCodigo { get; set; }
            public double CantidadIngreso { get; set; }
            public double Precio { get; set; }
            public int ProveedorId { get; set; }
            public string Remito { get; set; }
            public int OrdenCompra { get; set; }
            public int PedidoId { get; set; }
            public DateOnly FechaRemito { get; set; }
        }

        // POST: /Ingreso/Create
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] List<IngresoConPrecioDto> ingresosDto)
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
                foreach (var ingresoDto in ingresosDto)
                {
                    var itemCodigo = ingresoDto.ItemCodigo?.ToUpper();
                    Console.WriteLine($"Validando ingreso - ItemCodigo: {itemCodigo}, PedidoId: {ingresoDto.PedidoId}");
                    
                    // Verificar que el ítem existe
                    var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == itemCodigo);
                    if (item == null)
                    {
                        return BadRequest(new { error = $"El ítem con código {itemCodigo} no fue encontrado." });
                    }

                    Console.WriteLine($"Item encontrado - Id: {item.Id}, Codigo: {item.Codigo}");

                    // VALIDACIÓN CRÍTICA: Verificar que el ítem pertenece al pedido especificado
                    var pedido = await context.Pedido
                        .FirstOrDefaultAsync(p => p.NumeroPedido == ingresoDto.PedidoId && 
                                                 p.ItemCodigo == itemCodigo && 
                                                 p.Estado == "PENDIENTE");

                    if (pedido == null)
                    {
                        Console.WriteLine($"ERROR: El ítem {itemCodigo} no pertenece al pedido {ingresoDto.PedidoId} o el pedido no existe/está completado");
                        return BadRequest(new { error = $"El ítem {itemCodigo} no pertenece al pedido {ingresoDto.PedidoId} o el pedido no existe/está completado." });
                    }

                    Console.WriteLine($"Pedido encontrado - Id: {pedido.Id}, NumeroPedido: {pedido.NumeroPedido}");
                    
                    // Guardar el stock anterior para el Kardex
                    double stockAnterior = item.Stock;
                    
                    // Crear objeto Ingreso para guardar en BD (sin precio)
                    var ingreso = new Ingreso
                    {
                        ItemCodigo = itemCodigo,
                        ItemId = item.Id,
                        CantidadIngreso = ingresoDto.CantidadIngreso,
                        ProveedorId = ingresoDto.ProveedorId,
                        Remito = ingresoDto.Remito,
                        OrdenCompra = ingresoDto.OrdenCompra,
                        PedidoId = pedido.Id,
                        FechaRemito = ingresoDto.FechaRemito
                    };
                    
                    // Actualizar cantidad recibida en el pedido
                    pedido.Recibido += ingresoDto.CantidadIngreso;
                    
                    // Disminuir CantidadEnPedidos por la cantidad ingresada
                    item.CantidadEnPedidos -= ingresoDto.CantidadIngreso;
                    if (item.CantidadEnPedidos < 0) item.CantidadEnPedidos = 0;
                    
                    Console.WriteLine($"CantidadEnPedidos actualizada: {item.CantidadEnPedidos} (disminuyó en {ingresoDto.CantidadIngreso})");
                    
                    // Verificar si el pedido está completo
                    if (pedido.Recibido >= pedido.Cantidad)
                    {
                        pedido.Estado = "COMPLETADO";
                        Console.WriteLine($"Pedido completado - Estado: {pedido.Estado}");
                    }
                    
                    // Actualizar stock del ítem
                    item.Stock += ingresoDto.CantidadIngreso;
                    
                    // Actualizar precio del ítem si se proporciona un precio válido (desde el DTO)
                    if (ingresoDto.Precio > 0)
                    {
                        item.Precio = ingresoDto.Precio;
                        Console.WriteLine($"Precio actualizado: {item.Precio}");
                    }
                    
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
