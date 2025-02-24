using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;

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
                                        .ToList();
            return View("~/Views/MaterialesSalida/Index.cshtml", salidas);
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
            foreach (var salida in salidas)
            {
                var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == salida.ItemCodigo);
                if (item == null)
                {
                    return NotFound(new { error = $"El ítem con código {salida.ItemCodigo} no fue encontrado." });
                }

                // Actualiza el stock del ítem
                if (item.Stock < salida.Cantidad)
                {
                    return BadRequest(new { error = $"Stock insuficiente para el ítem {item.Codigo}." });
                }
                item.Stock -= salida.Cantidad;
                salida.ItemId = item.Id; // Asegúrate de establecer ItemId correctamente
                salida.Item = null; // Asegúrate de no enviar el objeto Item completo en la solicitud

                // Añade la salida a la base de datos
                context.Salida.Add(salida);
            }

            await context.SaveChangesAsync();
            return Ok(new { message = "Salidas registradas exitosamente." });
        }

    }
}
