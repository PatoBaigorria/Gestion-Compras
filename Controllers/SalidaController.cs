using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;

namespace Gestion_Compras.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class SalidaController : Controller
    {
        private readonly DataContext context;

        public SalidaController(DataContext context)
        {
            this.context = context;
        }

        // Acción para mostrar la lista de salidas 
        [HttpGet("Index")]
        public IActionResult Index()
        {
            var salidas = context.Salida.Include(s => s.Personal)
                                        .Include(s => s.Item)
                                        .OrderByDescending(s => s.FechaVale)
                                        .ToList();
            return View("~/Views/MaterialesSalida/Index.cshtml", salidas);
        }

        // GET: /Salida/ListJson
        [HttpGet("ListJson")]
        public async Task<IActionResult> ListJson(string searchTerm = "", int pagina = 1, int tamanoPagina = 100)
        {
            pagina = Math.Max(1, pagina);
            tamanoPagina = tamanoPagina <= 0 ? 100 : tamanoPagina;

            var query = context.Salida
                .Include(s => s.Personal)
                .Include(s => s.Item)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(s =>
                    (s.ItemCodigo ?? "").ToLower().Contains(term) ||
                    (s.Item != null && (s.Item.Descripcion ?? "").ToLower().Contains(term)) ||
                    (s.Personal != null && (s.Personal.NombreYApellido ?? "").ToLower().Contains(term))
                );
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(s => s.Id)
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(s => new
                {
                    itemCodigo = s.ItemCodigo,
                    descripcion = s.Item != null ? s.Item.Descripcion : "",
                    cantidad = s.Cantidad,
                    personal = s.Personal != null ? s.Personal.NombreYApellido : "",
                    fechaVale = s.FechaVale.ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            return Ok(new { items, total, pagina, tamanoPagina });
        }

        // GET: /Salida/AltaSalidas
        [HttpGet("AltaSalidas")]
        public IActionResult AltaSalidas()
        {
            var personalList = context.Personal.ToList();
            var itemList = context.Item
                                 .Select(i => new
                                 {
                                     Id = i.Id,
                                     Codigo = i.Codigo,
                                     Descripcion = i.Descripcion
                                 }).ToList();
            ViewBag.PersonalList = personalList;
            ViewBag.ItemList = itemList; // Pasar la lista de ítems a la vista

            return View("~/Views/MaterialesSalida/AltaSalidas.cshtml");
        }

        [HttpGet("BuscarItemPorCodigo")]
        public IActionResult BuscarItemPorCodigo(string codigo)
        {
            var item = context.Item.FirstOrDefault(i => i.Codigo == codigo);
            if (item == null)
            {
                return NotFound(new { error = "Ítem no encontrado." });
            }

            return Ok(item);
        }



        // POST: /Salida/Create
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] List<Salida> salidas)
        {
            // Obtener usuario logueado (Id)
            int? usuarioId = null;
            if (User?.Identity?.IsAuthenticated == true)
            {
                if (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid))
                    usuarioId = uid;
            }
            foreach (var salida in salidas)
            {
                var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == salida.ItemCodigo);
                if (item == null)
                {
                    return NotFound(new { error = $"El ítem con código {salida.ItemCodigo} no fue encontrado." });
                }

                // Verificar stock suficiente
                if (item.Stock < salida.Cantidad)
                {
                    return BadRequest(new { error = $"Stock insuficiente para el ítem {item.Codigo}." });
                }

                // Guardar el stock anterior para el Kardex
                double stockAnterior = item.Stock;

                // Actualizar el stock del ítem
                item.Stock -= salida.Cantidad;
                salida.ItemId = item.Id;
                salida.Item = null;

                // Crear registro en Kardex
                var kardexRegistro = new Kardex
                {
                    ItemId = item.Id,
                    StockIni = stockAnterior,
                    Cantidad = salida.Cantidad,
                    TipoDeMov = "Salida",
                    FechaRegistro = DateTime.Now,
                    FechaMov = salida.FechaVale,
                    UsuarioId = usuarioId
                };

                // Añadir la salida y el registro de Kardex a la base de datos
                context.Salida.Add(salida);
                context.Kardex.Add(kardexRegistro);
            }

            await context.SaveChangesAsync();
            return Ok(new { message = "Salidas registradas exitosamente." });
        }

    }
}
