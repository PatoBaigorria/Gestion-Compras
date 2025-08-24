using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Gestion_Compras.Models
{

	public class Item
	{
		[Key] 
		public int Id { get; set; }
		
		[Display(Name = "Código")] 
		public string? Codigo { get; set; }

		[Required] 
		public string Descripcion { get; set; }
		public double Stock { get; set; }
		public double PuntoDePedido { get; set; }
		public double Precio { get; set; }
		public bool Critico { get; set; } = true;
		public int CantidadEnPedidos { get; set; }
		public bool Activo { get; set; } = true;

		[ForeignKey(nameof(UnidadDeMedidaId))]
		public UnidadDeMedida? UnidadDeMedida { get; set; }
		public int UnidadDeMedidaId { get; set; }
		 
		[ForeignKey(nameof(SubFamiliaId))]
		public SubFamilia? SubFamilia { get; set; }
		public int SubFamiliaId { get; set; }
	}
}