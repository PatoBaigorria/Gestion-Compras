using System.ComponentModel.DataAnnotations;

namespace Gestion_Compras.Models
{
    public class Registro
    {
        [Key]
        [Display(Name = "ID del registro")]
        public int IdRegistro { get; set; }
        [Required]
        [Display(Name = "ID del usuario")]
        public int IdUsuario { get; set; }
        [Required]
        [Display(Name = "ID del recurso")]
        public int IdFila { get; set; } 
        [Display(Name = "Nombre de la tabla")]
        public string? NombreDeTabla { get; set; }
        [EmailAddress]
        [Display(Name = "Tipo de acción")]
        public string? TipoDeAccion { get; set; }
        [Required]
        [Display(Name = "Fecha de la acción")]
        public DateOnly FechaDeAccion { get; set; }
        [Display(Name = "Hora de la acción")]
        public TimeSpan HoraDeAccion { get; set; }
        [Display(Name="Usuario")]
        public Usuario Usuario {get; set;} = new Usuario();
    }
}