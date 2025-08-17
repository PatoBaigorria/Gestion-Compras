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
                                          .ToList(); 
            return View("~/Views/MaterialesIngreso/Index.cshtml", ingresos); 
        }


        // GET: /Ingreso/AltaIngresos
        [HttpGet("AltaIngresos")]
        public IActionResult AltaIngresos()
        {
            var personalList = context.Personal.ToList();
            var proveedorList = context.Proveedor.ToList(); // Obtener lista de proveedores
            var itemList = context.Item.ToList(); // Obtener lista de ítems
            ViewBag.PersonalList = personalList;
            ViewBag.ItemList = itemList; // Pasar la lista de ítems a la vista
            ViewBag.ProveedorList = proveedorList; // Pasar la lista de proveedores a la vista
            return View("~/Views/MaterialesIngreso/AltaIngresos.cshtml");
        }

        // POST: /Ingreso/Create
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] List<Ingreso> ingresos)
        {
            foreach (var ingreso in ingresos)
            {
                var item = await context.Item.FirstOrDefaultAsync(i => i.Codigo == ingreso.ItemCodigo);
                if (item == null)
                {
                    return NotFound(new { error = $"El ítem con código {ingreso.ItemCodigo} no fue encontrado." });
                }

                // Guardar el stock anterior para el Kardex
                double stockAnterior = item.Stock;

                // Actualizar el stock del item
                item.Stock += ingreso.CantidadIngreso;
                ingreso.ItemId = item.Id;
                ingreso.Item = null;

                // Crear registro en Kardex
                var kardexRegistro = new Kardex
                {
                    ItemId = item.Id,
                    StockIni = stockAnterior,
                    Cantidad = ingreso.CantidadIngreso,
                    TipoDeMov = "Ingreso",
                    FechaRegistro = DateTime.Now,
                    FechaMov = ingreso.FechaRemito  // Para ingresos: fecha del remito
                };

                // Añadir el ingreso y el registro de Kardex a la base de datos
                context.Ingreso.Add(ingreso);
                context.Kardex.Add(kardexRegistro);
            }

            await context.SaveChangesAsync();
            return Ok(new { message = "Ingresos registrados exitosamente." });
        }
    }
}
