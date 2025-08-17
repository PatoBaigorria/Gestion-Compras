using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gestion_Compras.Models
{

	public class Ajuste
	{
		[Key] 
		public int Id { get; set; }

		public string ItemCodigo { get; set; }
		 
		public int StockIni { get; set; }

        public int StockReal { get; set; }

        public string Observaciones { get; set; }

        public DateOnly FechaAjuste { get; set; }
	}
}