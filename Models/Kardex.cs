using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gestion_Compras.Models
{

    public class Kardex
    {
        [Key] 
        public int Id { get; set; }

        [ForeignKey(nameof(ItemId))]
        public Item? Item { get; set; }
        public int ItemId { get; set; }

        public double StockIni { get; set; }

        public double Cantidad { get; set; }

        public string TipoDeMov { get; set; }

        public DateTime FechaRegistro { get; set; }

        public DateOnly FechaMov { get; set; }

        // Auditoría: usuario que realizó el movimiento
        [ForeignKey(nameof(UsuarioId))]
        public Usuario? Usuario { get; set; }
        public int? UsuarioId { get; set; }
    }
}