using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;

namespace Gestion_Compras.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PedidoController : Controller
    {
        private readonly DataContext context;

        public PedidoController(DataContext context)
        {
            this.context = context;
        }

        // Vista principal de pedidos
        [HttpGet]
        public IActionResult Index()
        {
            return View("~/Views/Pedido/Index.cshtml");
        }

        // Lista de pedidos (JSON)
        [HttpGet("list")]
        public async Task<ActionResult<IEnumerable<object>>> GetPedidos()
        {
            var pedidos = await context.Pedido
                .Join(context.Item, p => p.ItemCodigo, i => i.Codigo, (p, i) => new { p, i })
                .Join(context.SubFamilia, pi => pi.i.SubFamiliaId, sf => sf.Id, (pi, sf) => new { pi.p, pi.i, sf })
                .Join(context.UnidadDeMedida, pis => pis.i.UnidadDeMedidaId, um => um.Id, (pis, um) => new { pis.p, pis.i, pis.sf, um })
                .Select(result => new
                {
                    result.p.Id,
                    result.p.NumeroPedido,
                    result.p.FechaPedido,
                    result.p.ItemCodigo,
                    ItemDescripcion = result.i.Descripcion,
                    result.p.Cantidad,
                    SubFamilia = result.sf.Descripcion,
                    UnidadMedida = result.um.Abreviatura,
                    result.p.Recibido,
                    result.p.Estado
                })
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            return Ok(pedidos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Pedido>> GetPedido(int id)
        {
            var pedido = await context.Pedido
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
            {
                return NotFound();
            }

            return pedido;
        }

        [HttpGet("siguiente-numero")]
        public async Task<ActionResult<int>> GetSiguienteNumero()
        {
            var ultimoNumeroPedido = await context.Pedido
                .MaxAsync(p => (int?)p.NumeroPedido) ?? 0;
            
            return Ok(ultimoNumeroPedido + 1);
        }

        [HttpPost]
        public async Task<ActionResult<Pedido>> PostPedido(Pedido pedido)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Verificar que el item existe por código
            var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == pedido.ItemCodigo);
            if (item == null)
            {
                return BadRequest("El ítem especificado no existe.");
            }

            // El ID se asigna automáticamente por la base de datos

            // Establecer valores por defecto
            pedido.FechaPedido = DateOnly.FromDateTime(DateTime.Now);
            pedido.Estado = "PENDIENTE";
            pedido.Recibido = 0;
            pedido.ItemCodigo = item.Codigo;
            pedido.UnidadDeMedidaId = item.UnidadDeMedidaId;
            pedido.SubFamiliaId = item.SubFamiliaId;

            context.Pedido.Add(pedido);
            await context.SaveChangesAsync();

            return Ok(new { message = "Pedido creado exitosamente", pedido });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutPedido(int id, Pedido pedido)
        {
            if (id != pedido.Id)
            {
                return BadRequest();
            }

            var pedidoExistente = await context.Pedido.FindAsync(id);
            if (pedidoExistente == null)
            {
                return NotFound();
            }

            // Actualizar solo los campos permitidos
            pedidoExistente.Cantidad = pedido.Cantidad;
            pedidoExistente.Recibido = pedido.Recibido;
            pedidoExistente.Estado = pedido.Estado;

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PedidoExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Ok(new { message = "Pedido actualizado exitosamente" });
        }

        [HttpPut("{id}/cambiar-estado")]
        public async Task<IActionResult> CambiarEstado(int id, [FromBody] string nuevoEstado)
        {
            var pedido = await context.Pedido.FindAsync(id);
            if (pedido == null)
            {
                return NotFound();
            }

            pedido.Estado = nuevoEstado;
            
            // Si se marca como recibido, actualizar la cantidad recibida
            if (nuevoEstado == "RECIBIDO" && pedido.Recibido == 0)
            {
                pedido.Recibido = pedido.Cantidad;
            }

            await context.SaveChangesAsync();
            return Ok(new { message = "Estado actualizado exitosamente" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePedido(int id)
        {
            var pedido = await context.Pedido.FindAsync(id);
            if (pedido == null)
            {
                return NotFound();
            }

            context.Pedido.Remove(pedido);
            await context.SaveChangesAsync();

            return Ok(new { message = "Pedido eliminado exitosamente" });
        }

        [HttpGet("items/{numeroPedido}")]
        public async Task<ActionResult<IEnumerable<object>>> GetItemsPedido(int numeroPedido)
        {
            try
            {
                var items = await context.Pedido
                    .Join(context.Item, p => p.ItemCodigo, i => i.Codigo, (p, i) => new { p, i })
                    .Where(pi => pi.p.NumeroPedido == numeroPedido)
                    .Select(pi => new
                    {
                        pi.p.Id,
                        pi.p.ItemCodigo,
                        ItemDescripcion = pi.i.Descripcion,
                        pi.p.Cantidad,
                        pi.p.Recibido,
                        pi.p.Estado
                    })
                    .OrderBy(x => x.ItemCodigo)
                    .ToListAsync();

                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Error al obtener ítems del pedido: " + ex.Message
                });
            }
        }

        [HttpPut("anular-item/{itemId}")]
        public async Task<IActionResult> AnularItemIndividual(int itemId)
        {
            try
            {
                var pedido = await context.Pedido.FindAsync(itemId);
                if (pedido == null)
                {
                    return NotFound(new { message = "Ítem de pedido no encontrado" });
                }

                // Verificar si ya está anulado
                if (pedido.Estado == "CANCELADO")
                {
                    return BadRequest(new { message = "El ítem ya está anulado" });
                }

                // Verificar si ya fue recibido
                if (pedido.Estado == "RECIBIDO" || pedido.Estado == "COMPLETADO")
                {
                    return BadRequest(new { message = "No se puede anular un ítem que ya fue recibido" });
                }

                // Anular el ítem
                pedido.Estado = "CANCELADO";
                
                // Revertir la cantidad en pedidos del item
                var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == pedido.ItemCodigo);
                if (item != null)
                {
                    var cantidadAnterior = item.CantidadEnPedidos;
                    item.CantidadEnPedidos = Math.Max(0, item.CantidadEnPedidos - pedido.Cantidad);
                    context.Item.Update(item);
                    
                    // Log para debugging
                    Console.WriteLine($"Anulación ítem individual - Código: {pedido.ItemCodigo}, Cantidad anterior: {cantidadAnterior}, Cantidad anulada: {pedido.Cantidad}, Nueva cantidad: {item.CantidadEnPedidos}");
                }

                await context.SaveChangesAsync();

                return Ok(new { 
                    message = $"Ítem {pedido.ItemCodigo} anulado exitosamente"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Error al anular el ítem: " + ex.Message
                });
            }
        }

        [HttpPut("anular/{numeroPedido}")]
        public async Task<IActionResult> AnularPedido(int numeroPedido)
        {
            try
            {
                // Buscar todos los pedidos con el número de pedido especificado
                var pedidos = await context.Pedido
                    .Where(p => p.NumeroPedido == numeroPedido)
                    .ToListAsync();

                if (!pedidos.Any())
                {
                    return NotFound(new { message = $"No se encontró el pedido N° {numeroPedido}" });
                }

                // Filtrar solo los pedidos que se pueden anular (PENDIENTE)
                var pedidosAnulables = pedidos.Where(p => p.Estado == "PENDIENTE").ToList();
                var pedidosCompletados = pedidos.Where(p => p.Estado == "RECIBIDO" || p.Estado == "COMPLETADO").Count();
                var pedidosYaAnulados = pedidos.Where(p => p.Estado == "CANCELADO").Count();

                if (!pedidosAnulables.Any())
                {
                    if (pedidosYaAnulados == pedidos.Count)
                    {
                        return BadRequest(new { message = $"El pedido N° {numeroPedido} ya está completamente anulado" });
                    }
                    else
                    {
                        return BadRequest(new { message = $"No hay ítems pendientes para anular en el pedido N° {numeroPedido}" });
                    }
                }

                // Anular solo los pedidos pendientes
                foreach (var pedido in pedidosAnulables)
                {
                    pedido.Estado = "CANCELADO";
                    
                    // Revertir la cantidad en pedidos del item
                    var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == pedido.ItemCodigo);
                    if (item != null)
                    {
                        var cantidadAnterior = item.CantidadEnPedidos;
                        item.CantidadEnPedidos = Math.Max(0, item.CantidadEnPedidos - pedido.Cantidad);
                        context.Item.Update(item);
                        
                        // Log para debugging
                        Console.WriteLine($"Anulación pedido completo - Código: {pedido.ItemCodigo}, Cantidad anterior: {cantidadAnterior}, Cantidad anulada: {pedido.Cantidad}, Nueva cantidad: {item.CantidadEnPedidos}");
                    }
                }

                await context.SaveChangesAsync();

                string mensaje = $"Se anularon {pedidosAnulables.Count} ítems del pedido N° {numeroPedido}.";
                if (pedidosCompletados > 0)
                {
                    mensaje += $" {pedidosCompletados} ítems ya completados no fueron afectados.";
                }

                return Ok(new { 
                    message = mensaje,
                    pedidosAnulados = pedidosAnulables.Count,
                    pedidosCompletados = pedidosCompletados,
                    pedidosYaAnulados = pedidosYaAnulados
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Error al anular el pedido: " + ex.Message
                });
            }
        }

        [HttpGet("items")]
        public async Task<ActionResult<IEnumerable<object>>> GetItems()
        {
            var items = await context.Item
                .Include(i => i.UnidadDeMedida)
                .Include(i => i.SubFamilia)
                .Where(i => i.Activo)
                .Select(i => new
                {
                    i.Id,
                    i.Codigo,
                    i.Descripcion,
                    UnidadMedida = i.UnidadDeMedida.Abreviatura,
                    SubFamilia = i.SubFamilia.Descripcion,
                    i.UnidadDeMedidaId,
                    i.SubFamiliaId
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("item-por-codigo")]
        public async Task<ActionResult<object>> GetItemPorCodigo(string codigo)
        {
            if (string.IsNullOrEmpty(codigo))
            {
                return BadRequest("El código es requerido");
            }

            // Debug: Buscar todos los items con código similar
            var todosLosItems = await context.Item
                .Where(i => i.Codigo != null && i.Codigo.Contains(codigo.ToUpper()))
                .Select(i => new { i.Codigo, i.Activo, i.Descripcion })
                .ToListAsync();

            var item = await context.Item
                .Include(i => i.UnidadDeMedida)
                .Include(i => i.SubFamilia)
                .Where(i => i.Activo && i.Codigo != null && i.Codigo == codigo.ToUpper())
                .Select(i => new
                {
                    i.Id,
                    i.Codigo,
                    i.Descripcion,
                    UnidadDeMedida = i.UnidadDeMedida != null ? i.UnidadDeMedida.Abreviatura : "",
                    i.Stock
                })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                // Verificar si existe pero está inactivo
                var itemInactivo = todosLosItems.FirstOrDefault(i => i.Codigo == codigo.ToUpper() && !i.Activo);
                if (itemInactivo != null)
                {
                    return Ok(new { 
                        success = false, 
                        message = $"El item {itemInactivo.Codigo} está inactivo",
                        inactivo = true
                    });
                }

                return Ok(new { 
                    success = false, 
                    message = "Item no encontrado",
                    debug = new {
                        codigoBuscado = codigo.ToUpper(),
                        itemsSimilares = todosLosItems
                    }
                });
            }

            return Ok(new { success = true, item = item });
        }

        [HttpPost("generar-pedido")]
        public async Task<ActionResult> GenerarPedido([FromBody] GenerarPedidoRequest request)
        {
            try
            {
                if (request.Items == null || !request.Items.Any())
                {
                    return BadRequest(new { success = false, message = "Debe incluir al menos un item en el pedido" });
                }

                // Obtener el siguiente número de pedido para todos los items
                var ultimoNumeroPedido = await context.Pedido
                    .MaxAsync(p => (int?)p.NumeroPedido) ?? 0;
                var numeroPedido = ultimoNumeroPedido + 1;

                // Crear los pedidos individuales para cada item con el mismo número de pedido
                var pedidosCreados = new List<Pedido>();

                foreach (var itemRequest in request.Items)
                {
                    var item = await context.Item.FindAsync(itemRequest.ItemId);
                    if (item == null)
                    {
                        return BadRequest(new { success = false, message = $"Item con ID {itemRequest.ItemId} no encontrado" });
                    }

                    var pedido = new Pedido
                    {
                        NumeroPedido = numeroPedido,
                        FechaPedido = DateOnly.FromDateTime(DateTime.Now),
                        ItemCodigo = item.Codigo ?? "",
                        UnidadDeMedidaId = item.UnidadDeMedidaId,
                        SubFamiliaId = item.SubFamiliaId,
                        Cantidad = itemRequest.Cantidad,
                        Recibido = 0,
                        Estado = "PENDIENTE"
                    };

                    // Actualizar CantidadEnPedidos del item
                    var cantidadAnterior = item.CantidadEnPedidos;
                    item.CantidadEnPedidos += itemRequest.Cantidad;
                    context.Item.Update(item);
                    
                    // Log para debugging
                    Console.WriteLine($"[GENERAR PEDIDO] Item {item.Codigo}: CantidadEnPedidos {cantidadAnterior} -> {item.CantidadEnPedidos} (+{itemRequest.Cantidad})");

                    context.Pedido.Add(pedido);
                    pedidosCreados.Add(pedido);
                }

                await context.SaveChangesAsync();

                return Ok(new { 
                    success = true, 
                    message = $"Pedido N° {numeroPedido} generado exitosamente con {pedidosCreados.Count} items",
                    numeroPedido = numeroPedido,
                    pedidosCreados = pedidosCreados.Count
                });
            }
            catch (Exception ex)
            {
                // Log detallado del error
                var innerMessage = ex.InnerException?.Message ?? "No inner exception";
                var fullMessage = $"Error: {ex.Message} | Inner: {innerMessage}";
                
                return StatusCode(500, new { 
                    success = false, 
                    message = "Error interno del servidor: " + ex.Message,
                    innerException = innerMessage,
                    stackTrace = ex.StackTrace
                });
            }
        }

        public class GenerarPedidoRequest
        {
            public List<ItemPedidoRequest> Items { get; set; } = new List<ItemPedidoRequest>();
        }

        public class ItemPedidoRequest
        {
            public int ItemId { get; set; }
            public int Cantidad { get; set; }
        }

        [HttpPost("actualizar-numeros-pedido")]
        public async Task<ActionResult> ActualizarNumerosPedido()
        {
            try
            {
                var pedidosSinNumero = await context.Pedido
                    .Where(p => p.NumeroPedido == 0)
                    .OrderBy(p => p.Id)
                    .ToListAsync();

                if (!pedidosSinNumero.Any())
                {
                    return Ok(new { success = true, message = "No hay pedidos sin número para actualizar" });
                }

                // Asignar números de pedido basados en el Id
                foreach (var pedido in pedidosSinNumero)
                {
                    pedido.NumeroPedido = pedido.Id;
                }

                await context.SaveChangesAsync();

                return Ok(new { 
                    success = true, 
                    message = $"Se actualizaron {pedidosSinNumero.Count} pedidos con números de pedido",
                    pedidosActualizados = pedidosSinNumero.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = "Error al actualizar números de pedido: " + ex.Message
                });
            }
        }

        [HttpGet("verificar-item/{codigo}")]
        public async Task<ActionResult> VerificarItem(string codigo)
        {
            try
            {
                var item = await context.Item
                    .Where(i => i.Codigo == codigo.ToUpper())
                    .Select(i => new
                    {
                        i.Id,
                        i.Codigo,
                        i.Descripcion,
                        i.CantidadEnPedidos,
                        i.Stock,
                        i.Activo
                    })
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    return Ok(new { 
                        existe = false, 
                        message = $"El ítem {codigo.ToUpper()} no existe en la tabla Item"
                    });
                }

                return Ok(new { 
                    existe = true, 
                    item = item,
                    message = $"Ítem {codigo.ToUpper()} encontrado"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Error al verificar ítem: " + ex.Message
                });
            }
        }

        private bool PedidoExists(int id)
        {
            return context.Pedido.Any(e => e.Id == id);
        }
    }
}