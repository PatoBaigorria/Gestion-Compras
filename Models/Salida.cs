using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gestion_Compras.Models
{

	public class Salida
	{
		[Key] 
		public int Id { get; set; }

		public string ItemCodigo { get; set; }

		[ForeignKey(nameof(ItemId))] 
		public Item? Item { get; set; } 
		public int ItemId { get; set; }
		 
		[ForeignKey(nameof(PersonalId))]
		public Personal? Personal { get; set; }
		public int PersonalId { get; set; }

        public int Cantidad { get; set; }

        public DateOnly Fecha { get; set; }
	}
}