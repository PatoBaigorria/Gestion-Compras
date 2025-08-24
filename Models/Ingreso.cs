using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gestion_Compras.Models
{

	public class Ingreso
	{
		[Key] 
		public int Id { get; set; }

		[Required]
		public string ItemCodigo { get; set; } = string.Empty;
		[ForeignKey(nameof(ItemId))] 
		public Item? Item { get; set; } 
		public int ItemId { get; set; }
		 
		[ForeignKey(nameof(ProveedorId))]
		public Proveedor? Proveedor { get; set; }
		public int ProveedorId { get; set; }

        public int CantidadIngreso { get; set; }

        [Required]
        public string Remito { get; set; } = string.Empty;

        public int OrdenCompra { get; set; }

        public int PedidoId { get; set; }

        public DateOnly FechaRemito { get; set; }
	}
}