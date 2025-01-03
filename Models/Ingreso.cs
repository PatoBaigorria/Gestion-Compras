using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gestion_Compras.Models
{

	public class Ingreso
	{
		[Key] 
		public int Id { get; set; }

		public string ItemCodigo { get; set; }
		[ForeignKey(nameof(ItemId))] 
		public Item? Item { get; set; } 
		public int ItemId { get; set; }
		 
		[ForeignKey(nameof(ProveedorId))]
		public Proveedor? Proveedor { get; set; }
		public int ProveedorId { get; set; }

        public int CantidadIngreso { get; set; }

        public string Remito { get; set; }

        public int OrdenCompra { get; set; }

        public int Pedido { get; set; }

        public DateOnly Fecha { get; set; }
	}
}