using Microsoft.AspNetCore.Mvc;
using Gestion_Compras.Models;
using Microsoft.EntityFrameworkCore;
using Gestion_Compras.ViewModels;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;


namespace Gestion_Compras.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class FamiliaSubFamiliaController : Controller
    {
        private readonly DataContext context;

        public FamiliaSubFamiliaController(DataContext context)
        {
            this.context = context;
        }



        // Acción GET para mostrar el formulario
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var familias = await context.Familia
                    .Select(f => new Familia { Id = f.Id, Codigo = f.Codigo, Descripcion = f.Descripcion })
                    .ToListAsync();

                var subfamilias = await context.SubFamilia
                    .Select(sf => new SubFamilia { Id = sf.Id, Codigo = sf.Codigo, Descripcion = sf.Descripcion })
                    .ToListAsync();

                var unidadesDeMedida = await context.UnidadDeMedida
                    .Select(um => new UnidadDeMedida { Id = um.Id, Abreviatura = um.Abreviatura })
                    .ToListAsync();

                var modelo = new FamiliaSubFamiliaViewModel
                {
                    FamiliaList = familias,
                    SubFamiliaList = subfamilias,
                    UnidadDeMedidaList = unidadesDeMedida,
                    FamiliaSeleccionadaId = null // Inicialmente no hay selección
                };

                return View(modelo);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }



        [HttpGet]
        public async Task<IActionResult> ObtenerFamilias()
        {
            try
            {
                var familias = await context.Familia
                    .Select(f => new { f.Id, f.Codigo, f.Descripcion })
                    .ToListAsync();

                return Json(familias);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("ObtenerSubfamilias/{familiaId}")]
        public async Task<ActionResult> ObtenerSubfamilias(int familiaId)
        {
            var subfamilias = await context.SubFamilia
            .Where(sf => sf.FamiliaId == familiaId)
            .ToListAsync();
            return Ok(subfamilias);
        }

        [HttpGet("GetSubFamiliasByFamiliaId")]
        public async Task<IActionResult> GetSubFamiliasByFamiliaId(int familiaId)
        {
            try
            {
                var subFamilias = await context.SubFamilia
                    .Where(sf => sf.FamiliaId == familiaId)
                    .OrderBy(sf => sf.Descripcion)
                    .Select(sf => new { 
                        id = sf.Id, 
                        descripcion = sf.Descripcion 
                    })
                    .ToListAsync();

                return Ok(subFamilias);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al cargar las subfamilias: " + ex.Message });
            }
        }


        // Acción para crear solo una familia
        [HttpPost("CrearFamilia")]
        public async Task<ActionResult> CrearFamilia([FromBody] Familia familia)
        {
            if (familia == null || string.IsNullOrWhiteSpace(familia.Codigo))
            {
                return BadRequest(new { error = "El código de familia no puede estar vacío." });
            }

            var familiaExistente = await context.Familia.FirstOrDefaultAsync(f => f.Codigo == familia.Codigo);
            if (familiaExistente != null)
            {
                return Conflict(new { error = "El código de familia ya existe." });
            }

            try
            {
                context.Familia.Add(familia);
                await context.SaveChangesAsync();
                return Ok(new { message = "Familia creada exitosamente.", familia });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error interno del servidor", detalles = ex.Message });
            }
        }


        // Acción para crear una subfamilia
        [HttpPost("CrearSubfamilia")]
        public async Task<ActionResult> CrearSubfamilia([FromBody] SubFamiliaRequest modelo)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { error = "Los datos del formulario no son válidos.", detalles = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
            }

            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                // Validar existencia de la familia
                var familiaExistente = await context.Familia.FirstOrDefaultAsync(f => f.Id == modelo.FamiliaId);
                if (familiaExistente == null)
                {
                    return NotFound(new { error = "La familia no existe." });
                }

                // Validar que la descripción de la subfamilia no esté vacía
                if (string.IsNullOrWhiteSpace(modelo.Descripcion))
                {
                    return BadRequest(new { error = "La descripción de la subfamilia no puede estar vacía." });
                }

                // Calcular el nuevo código para la subfamilia basado en la familia
                var subFamilias = await context.SubFamilia.Where(sf => sf.FamiliaId == modelo.FamiliaId).ToListAsync();
                var maxCodigo = subFamilias
                    .Select(sf => int.TryParse(sf.Codigo.Substring(familiaExistente.Codigo.Length), out var result) ? result : 0)
                    .DefaultIfEmpty(0)
                    .Max();
                var nuevoCodigo = $"{familiaExistente.Codigo}{(maxCodigo + 1):D2}";

                // Crear y guardar la nueva subfamilia
                var nuevaSubFamilia = new SubFamilia
                {
                    FamiliaId = modelo.FamiliaId,
                    Codigo = nuevoCodigo,
                    Descripcion = modelo.Descripcion
                };
                context.SubFamilia.Add(nuevaSubFamilia);
                await context.SaveChangesAsync();

                // Confirmar la transacción
                await transaction.CommitAsync();

                return Ok(new { message = "SubFamilia creada exitosamente.", subFamilia = nuevaSubFamilia });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { error = "Error interno del servidor", detalles = ex.Message });
            }
        }
    }

    public class SubFamiliaRequest
    {
        public int FamiliaId { get; set; }
        public string Descripcion { get; set; }
    }
}