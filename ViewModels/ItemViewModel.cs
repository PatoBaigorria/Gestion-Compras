using System.ComponentModel.DataAnnotations;

namespace Gestion_Compras.ViewModels
{
    public class ItemViewModel
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "El código es requerido")]
        [StringLength(20, ErrorMessage = "El código no puede tener más de 20 caracteres")]
        public string Codigo { get; set; }
        
        [Required(ErrorMessage = "La descripción es requerida")]
        [StringLength(255, ErrorMessage = "La descripción no puede tener más de 255 caracteres")]
        public string Descripcion { get; set; }
        
        [Required(ErrorMessage = "La unidad de medida es requerida")]
        public int UnidadDeMedidaId { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "El punto de pedido no puede ser negativo")]
        public double? PuntoDePedido { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "El precio no puede ser negativo")]
        public double? Precio { get; set; }
        
        public bool Critico { get; set; }
        
        [Required(ErrorMessage = "La subfamilia es requerida")]
        public int SubFamiliaId { get; set; }
        
        public bool Activo { get; set; } = true;
    }
}
