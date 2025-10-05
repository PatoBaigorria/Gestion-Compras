using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Globalization;

namespace Gestion_Compras.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PedidoController : Controller
    {
        private readonly DataContext context;
        private readonly IWebHostEnvironment env;

        public PedidoController(DataContext context, IWebHostEnvironment env)
        {
            this.context = context;
            this.env = env;
            
            // Configurar licencia QuestPDF (respaldo por si no se configuró en Program.cs)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // Vista principal de pedidos
        [HttpGet]
        public IActionResult Index()
        {
            return View("~/Views/Pedido/Index.cshtml");
        }

        // Lista de pedidos (JSON)
        [HttpGet("lista")]
        public async Task<ActionResult> GetPedidos(int pagina = 1, int tamanoPagina = 100, string filtroGeneral = "")
        {
            pagina = Math.Max(1, pagina);
            tamanoPagina = tamanoPagina <= 0 ? 100 : tamanoPagina;

            // Construir query base
            var query = context.Pedido.AsNoTracking();

            // Aplicar filtro general si existe
            if (!string.IsNullOrEmpty(filtroGeneral))
            {
                var filtroLower = filtroGeneral.ToLower();
                query = query.Where(p => 
                    (p.ItemCodigo != null && p.ItemCodigo.ToLower().Contains(filtroLower)) ||
                    p.Estado.ToLower().Contains(filtroLower) ||
                    p.NumeroPedido.ToString().Contains(filtroLower) ||
                    p.Cantidad.ToString().Contains(filtroLower) ||
                    p.Recibido.ToString().Contains(filtroLower)
                );
            }

            // Total de registros (con filtro aplicado)
            var total = await query.CountAsync();

            // 1) Página de pedidos (sin joins)
            var pagePedidos = await query
                .OrderByDescending(p => p.Id)
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(p => new
                {
                    p.Id,
                    p.NumeroPedido,
                    FechaPedido = p.FechaPedido.ToString("dd/MM/yyyy"),
                    p.ItemCodigo,
                    p.Cantidad,
                    p.Recibido,
                    p.Estado,
                    p.UsuarioId
                })
                .ToListAsync();

            if (pagePedidos.Count == 0)
            {
                return Ok(new { items = new object[0], total, pagina, tamanoPagina });
            }

            // 2) Resolver dependencias en lotes
            var codigos = pagePedidos.Select(x => x.ItemCodigo).Distinct().ToList();
            var itemsDict = await context.Item
                .AsNoTracking()
                .Where(i => codigos.Contains(i.Codigo))
                .Select(i => new { i.Codigo, i.Descripcion, i.UnidadDeMedidaId, i.SubFamiliaId })
                .ToDictionaryAsync(i => i.Codigo!, i => i);

            var unidadIds = itemsDict.Values.Select(v => v.UnidadDeMedidaId).Distinct().ToList();
            var unidadesDict = await context.UnidadDeMedida
                .AsNoTracking()
                .Where(um => unidadIds.Contains(um.Id))
                .ToDictionaryAsync(um => um.Id, um => um.Abreviatura);

            var subFamiliaIds = itemsDict.Values.Select(v => v.SubFamiliaId).Distinct().ToList();
            var subFamiliasDict = await context.SubFamilia
                .AsNoTracking()
                .Where(sf => subFamiliaIds.Contains(sf.Id))
                .ToDictionaryAsync(sf => sf.Id, sf => sf.Descripcion);

            var usuarioIds = pagePedidos.Select(p => p.UsuarioId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var usuariosDict = await context.Usuario
                .AsNoTracking()
                .Where(u => usuarioIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => (u.Apellido + " " + u.Nombre));

            // 3) Proyección final
            var items = pagePedidos.Select(p =>
            {
                var infoItem = (p.ItemCodigo != null && itemsDict.TryGetValue(p.ItemCodigo, out var ii)) ? ii : null;
                var unidad = (infoItem != null && unidadesDict.TryGetValue(infoItem.UnidadDeMedidaId, out var abrev)) ? abrev : "";
                var subfam = (infoItem != null && subFamiliasDict.TryGetValue(infoItem.SubFamiliaId, out var sfDesc)) ? sfDesc : "";
                var usuarioNombre = (p.UsuarioId.HasValue && usuariosDict.TryGetValue(p.UsuarioId.Value, out var nombre)) ? nombre : "";
                return new
                {
                    p.Id,
                    p.NumeroPedido,
                    p.FechaPedido,
                    ItemCodigo = p.ItemCodigo,
                    ItemDescripcion = infoItem?.Descripcion ?? "",
                    p.Cantidad,
                    SubFamilia = subfam,
                    UnidadMedida = unidad,
                    p.Recibido,
                    p.Estado,
                    Usuario = usuarioNombre
                };
            }).ToList();

            return Ok(new { items, total, pagina, tamanoPagina });
        }

        [HttpGet("pedido/{id}")]
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

        [Authorize(Roles = "Administrador")]
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
            // Auditoría: usuario
            if (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uidPost))
            {
                pedido.UsuarioId = uidPost;
            }

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

        [Authorize(Roles = "Administrador")]
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

        [Authorize(Roles = "Administrador")]
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

        [Authorize(Roles = "Administrador")]
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

        [Authorize(Roles = "Administrador")]
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

        [HttpGet("pdf/{numeroPedido}")]
        public async Task<IActionResult> GenerarPdfPedido(int numeroPedido)
        {
            try
            {
                // 1) Traer pedidos por número (sin joins)
                var pedidos = await context.Pedido
                    .Where(p => p.NumeroPedido == numeroPedido)
                    .OrderBy(p => p.Id)
                    .Select(p => new
                    {
                        p.NumeroPedido,
                        p.FechaPedido,
                        Codigo = p.ItemCodigo,
                        p.Cantidad,
                        p.Estado
                    })
                    .ToListAsync();

                if (!pedidos.Any())
                {
                    return NotFound(new { message = $"No se encontraron ítems para el pedido N° {numeroPedido}" });
                }

                // 2) Cargar datos de ítems por código en una sola consulta
                var codigos = pedidos.Select(x => x.Codigo).Distinct().ToList();
                var itemsDict = await context.Item
                    .Where(i => codigos.Contains(i.Codigo))
                    .Select(i => new { i.Codigo, i.Descripcion, i.UnidadDeMedidaId, i.SubFamiliaId, i.Precio })
                    .ToDictionaryAsync(i => i.Codigo!, i => i);

                // 3) Cargar abreviaturas de unidades para los ítems involucrados
                var unidadIds = itemsDict.Values.Select(v => v.UnidadDeMedidaId).Distinct().ToList();
                var unidadesDict = await context.UnidadDeMedida
                    .Where(um => unidadIds.Contains(um.Id))
                    .ToDictionaryAsync(um => um.Id, um => um.Abreviatura);

                // 3b) Cargar códigos de subfamilias para los ítems involucrados
                var subFamiliaIds = itemsDict.Values.Select(v => v.SubFamiliaId).Distinct().ToList();
                var subFamiliasDict = await context.SubFamilia
                    .Where(sf => subFamiliaIds.Contains(sf.Id))
                    .ToDictionaryAsync(sf => sf.Id, sf => sf.Descripcion);

                // 4) Proyectar lista final en memoria
                var items = pedidos
                    .Select(p =>
                    {
                        var info = itemsDict.TryGetValue(p.Codigo, out var val) ? val : null;
                        var unidad = (info != null && unidadesDict.TryGetValue(info.UnidadDeMedidaId, out var abrev)) ? abrev : "";
                        var descripcion = info?.Descripcion ?? "";
                        var equipoCodigo = info != null && subFamiliasDict.TryGetValue(info.SubFamiliaId, out var sfCodigo) ? sfCodigo : "";
                        return new
                        {
                            p.NumeroPedido,
                            p.FechaPedido,
                            p.Codigo,
                            Descripcion = descripcion,
                            p.Cantidad,
                            Unidad = unidad,
                            EquipoCodigo = equipoCodigo,
                            PrecioUnitarioReferencia = info?.Precio ?? 0.0
                        };
                    })
                    .ToList();

                var fecha = items.First().FechaPedido;
                // Obtener usuario logueado (nombre + apellido si existen en claims)
                var nombre = HttpContext?.User?.FindFirst(ClaimTypes.GivenName)?.Value;
                var apellido = HttpContext?.User?.FindFirst(ClaimTypes.Surname)?.Value;
                var solicitante = (!string.IsNullOrWhiteSpace(nombre) || !string.IsNullOrWhiteSpace(apellido))
                    ? string.Join(" ", new[] { nombre, apellido }.Where(s => !string.IsNullOrWhiteSpace(s)))
                    : (HttpContext?.User?.Identity?.Name ?? "");

                // Cargar logo: intenta logoFiasa.png y si es inválido o falta, usa fallback logo.png. Acepta PNG/JPG.
                byte[]? logoBytes = null;
                try
                {
                    var webRoot = env.WebRootPath ?? string.Empty;
                    var primary = Path.Combine(webRoot, "images", "logoFiasa.png");
                    var fallback = Path.Combine(webRoot, "images", "logo.png");

                    // Función local de validación básica (PNG/JPG)
                    static bool EsImagenSoportada(byte[] bytes)
                    {
                        // PNG: 89 50 4E 47 0D 0A 1A 0A
                        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
                            return true;
                        // JPG: FF D8 FF
                        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                            return true;
                        return false;
                    }

                    if (!string.IsNullOrEmpty(webRoot) && System.IO.File.Exists(primary))
                    {
                        var bytes = System.IO.File.ReadAllBytes(primary);
                        if (EsImagenSoportada(bytes))
                            logoBytes = bytes;
                    }
                    // Si el primario no es válido o no cargó, probar fallback
                    if (logoBytes == null && !string.IsNullOrEmpty(webRoot) && System.IO.File.Exists(fallback))
                    {
                        var bytes = System.IO.File.ReadAllBytes(fallback);
                        if (EsImagenSoportada(bytes))
                            logoBytes = bytes;
                    }
                }
                catch { /* sin bloqueo si falla el logo */ }

                // Construir documento PDF moderno y profesional
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(20);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontFamily("Arial"));

                        // Header moderno con gradiente visual
                        page.Header().Height(120).Background("#f8f9fa").Padding(20).Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                // Logo
                                if (logoBytes != null)
                                {
                                    row.ConstantItem(80).Height(60).Image(logoBytes);
                                }
                                
                                // Título principal
                                row.RelativeItem().AlignCenter().Column(titleCol =>
                                {
                                    titleCol.Item().AlignCenter().Text("NOTA DE PEDIDO")
                                        .FontSize(24).Bold().FontColor("#2c3e50");
                                    titleCol.Item().AlignCenter().PaddingTop(5).Text($"N° {numeroPedido}")
                                        .FontSize(16).SemiBold().FontColor("#3498db");
                                });
                                
                                // Info de referencia
                                row.ConstantItem(120).Column(refCol =>
                                {
                                    refCol.Item().AlignRight().Text("Ref.: P-COM-01").FontSize(10).FontColor("#7f8c8d");
                                    refCol.Item().AlignRight().Text("Rev. 03 Form. 91").FontSize(10).FontColor("#7f8c8d");
                                    refCol.Item().AlignRight().PaddingTop(10).Text("Hoja 1/1")
                                        .FontSize(12).SemiBold().FontColor("#2c3e50");
                                });
                            });
                        });

                        // Contenido principal
                        page.Content().Padding(10).Column(col =>
                        {
                            // Información del pedido en cards
                            col.Item().PaddingBottom(20).Row(row =>
                            {
                                // Card Solicitante
                                row.RelativeItem().Padding(5).Background("#ecf0f1").Border(1).BorderColor("#bdc3c7")
                                    .Padding(15).Column(cardCol =>
                                    {
                                        cardCol.Item().Text("SOLICITANTE").FontSize(9).Bold().FontColor("#7f8c8d");
                                        cardCol.Item().PaddingTop(5).Text(solicitante).FontSize(13).SemiBold().FontColor("#2c3e50");
                                    });
                                
                                row.ConstantItem(20); // Espaciado
                                
                                // Card Fecha a la derecha
                                row.ConstantItem(200).Padding(5).Background("#ecf0f1").Border(1).BorderColor("#bdc3c7")
                                    .Padding(15).Column(cardCol =>
                                    {
                                        cardCol.Item().AlignRight().Text("FECHA DE PEDIDO").FontSize(9).Bold().FontColor("#7f8c8d");
                                        cardCol.Item().PaddingTop(5).AlignRight().Text($"{fecha:dd/MM/yyyy}").FontSize(13).SemiBold().FontColor("#2c3e50");
                                    });
                            });

                            // Tabla moderna con mejor diseño
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(80);   // Código
                                    columns.RelativeColumn(4);   // Descripción
                                    columns.ConstantColumn(80);  // Unidad
                                    columns.ConstantColumn(80);  // Cantidad
                                    columns.ConstantColumn(100); // Equipo-Código
                                });

                                // Encabezado moderno
                                table.Header(header =>
                                {
                                    header.Cell().Element(ModernHeader).Text("CÓDIGO").FontColor("#ffffff");
                                    header.Cell().Element(ModernHeader).Text("DESCRIPCIÓN").FontColor("#ffffff");
                                    header.Cell().Element(ModernHeader).Text("UNIDAD").FontColor("#ffffff");
                                    header.Cell().Element(ModernHeader).Text("CANTIDAD").FontColor("#ffffff");
                                    header.Cell().Element(ModernHeader).Text("EQUIPO-CÓDIGO").FontColor("#ffffff");

                                    static IContainer ModernHeader(IContainer container)
                                        => container
                                            .Background("#34495e")
                                            .Padding(12)
                                            .DefaultTextStyle(x => x.Bold().FontSize(9));
                                });

                                // Filas con alternancia de colores
                                var isEven = false;
                                foreach (var it in items)
                                {
                                    var bgColor = isEven ? "#f8f9fa" : "#ffffff";
                                    
                                    table.Cell().Element(container => ModernCell(container, bgColor)).Text(it.Codigo).FontSize(8);
                                    table.Cell().Element(container => ModernCell(container, bgColor)).Text(it.Descripcion).FontSize(8);
                                    table.Cell().Element(container => ModernCell(container, bgColor)).AlignCenter().Text(it.Unidad).FontSize(8);
                                    table.Cell().Element(container => ModernCell(container, bgColor)).AlignCenter().Text(it.Cantidad.ToString()).FontSize(8).SemiBold();
                                    table.Cell().Element(container => ModernCell(container, bgColor)).AlignCenter().Text(it.EquipoCodigo).FontSize(8);
                                    
                                    isEven = !isEven;
                                }

                                static IContainer ModernCell(IContainer container, string backgroundColor)
                                    => container
                                        .Background(backgroundColor)
                                        .Border(0.5f)
                                        .BorderColor("#dee2e6")
                                        .Padding(10);
                            });

                        });

                        // Footer moderno
                        page.Footer().Height(40).Background("#34495e").Padding(10).Row(row =>
                        {
                            row.RelativeItem().AlignLeft().Text("Gestión Abastecimiento Pañol")
                                .FontSize(10).FontColor("#ffffff");
                            row.RelativeItem().AlignRight().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(10).FontColor("#ffffff");
                        });
                    });
                });

                var pdfBytes = document.GeneratePdf();
                var fileName = $"Pedido_{numeroPedido}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error al generar PDF del pedido: " + ex.Message
                });
            }
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

        [Authorize(Roles = "Administrador")]
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
                        Estado = "PENDIENTE",
                        UsuarioId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uidGen) ? uidGen : null
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