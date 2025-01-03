using Gestion_Compras.Models;

namespace Gestion_Compras.ViewModels
{
    public class FamiliaSubFamiliaViewModel
    {
        public Familia Familia { get; set; } // Para el modal de crear familia
        public SubFamilia SubFamilia { get; set; } // Para crear subfamilia
        public UnidadDeMedida UnidadDeMedida { get; set; }
        public IEnumerable<Familia> FamiliaList { get; set; } // Lista de familias para el combo box
        public IEnumerable<SubFamilia> SubFamiliaList { get; set; } // Lista de subfamilias para consulta
        public IEnumerable<UnidadDeMedida> UnidadDeMedidaList { get; set; } 
        public int? FamiliaSeleccionadaId { get; set; } // Propiedad para mantener seleccionada la familia en caso de errores
    }
}
