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

                // Verificar si ya está anulado
                if (pedidos.All(p => p.Estado == "CANCELADO"))
                {
                    return BadRequest(new { message = $"El pedido N° {numeroPedido} ya está anulado" });
                }

                // Verificar si algún pedido ya fue recibido
                if (pedidos.Any(p => p.Estado == "RECIBIDO" || p.Estado == "COMPLETADO"))
                {
                    return BadRequest(new { message = $"No se puede anular el pedido N° {numeroPedido} porque algunos ítems ya fueron recibidos" });
                }

                // Anular todos los pedidos con ese número
                foreach (var pedido in pedidos)
                {
                    pedido.Estado = "CANCELADO";
                    
                    // Revertir la cantidad en pedidos del item
                    var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == pedido.ItemCodigo);
                    if (item != null)
                    {
                        item.CantidadEnPedidos = Math.Max(0, item.CantidadEnPedidos - pedido.Cantidad);
                        context.Item.Update(item);
                    }
                }

                await context.SaveChangesAsync();

                return Ok(new { 
                    message = $"Pedido N° {numeroPedido} anulado exitosamente. Se anularon {pedidos.Count} ítems.",
                    pedidosAnulados = pedidos.Count
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
                    item.CantidadEnPedidos += itemRequest.Cantidad;
                    context.Item.Update(item);

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

        private bool PedidoExists(int id)
        {
            return context.Pedido.Any(e => e.Id == id);
        }
    }
}