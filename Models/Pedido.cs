using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gestion_Compras.Models
{

	public class Pedido
	{
		[Key] 
		public int Id { get; set; }

        public int NumPedido { get; set; }

		public string ItemCodigo { get; set; }
		[ForeignKey(nameof(ItemId))] 

		public Item? Item { get; set; } 
		public int ItemId { get; set; }

		public int UnidadDeMedidaId { get; set; }
        
		public int Cantidad { get; set; }

        public int Recibido { get; set; }

        public int  SubFamiliaId { get; set; }

        public string Estado { get; set; }

        public DateOnly FechaPedido { get; set; }
	}
}