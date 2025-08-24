using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;

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
                                          .Select(i => new
                                          {
                                              i.Id,
                                              i.ItemCodigo,
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

        // POST: /Ingreso/Create
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] List<Ingreso> ingresos)
        {
            try
            {
                foreach (var ingreso in ingresos)
                {
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
                    item.CantidadEnPedidos -= (int)ingreso.CantidadIngreso;
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
                        FechaRegistro = DateTime.Now
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
