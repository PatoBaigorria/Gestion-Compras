using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gestion_Compras.Models
{

	public class Pedido
	{
		[Key] 
		public int Id { get; set; }

		[Required]
		public int NumeroPedido { get; set; }

		[Required]
		public string ItemCodigo { get; set; } = string.Empty;

		public int UnidadDeMedidaId { get; set; }
        
		public double Cantidad { get; set; }

        public double Recibido { get; set; }

        public int  SubFamiliaId { get; set; }

        [Required]
        public string Estado { get; set; } = string.Empty;

        public DateOnly FechaPedido { get; set; }

        // Auditoría: usuario que generó el pedido
        [ForeignKey(nameof(UsuarioId))]
        public Usuario? Usuario { get; set; }
        public int? UsuarioId { get; set; }
	}
}