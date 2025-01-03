using System.ComponentModel.DataAnnotations;


namespace Gestion_Compras.Models
{

	public class Proveedor
	{
		[Key]
		[Display(Name = "Proveedor")]
		public int Id { get; set; }

		[Required]
		public string RazonSocial { get; set; }
		
	}
}