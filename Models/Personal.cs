using System.ComponentModel.DataAnnotations;


namespace Gestion_Compras.Models
{

	public class Personal
	{
		[Key]
		[Display(Name = "Personal")]
		public int Id { get; set; }

        [Required]
        public string NombreYApellido { get; set; }

        [Required]
        public string DNI { get; set; }
        
        [Required]
        public string Sector { get; set; }
		
	}
}