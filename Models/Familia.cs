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

	public class Familia
	{
		[Key]
		public int Id { get; set; }

		public string? Codigo { get; set; }

		[Required]
		public string Descripcion { get; set; }


	}


}