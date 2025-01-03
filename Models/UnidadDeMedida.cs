using System.ComponentModel.DataAnnotations;


namespace Gestion_Compras.Models
{

	public class UnidadDeMedida
	{
		[Key]
		[Display(Name = "Unidad de Medida")]
		public int Id { get; set; }

		[Required]
		public string Abreviatura { get; set; }
		
		[Required]
        public string Descripcion { get; set; }
        
	}
}