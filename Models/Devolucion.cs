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

    public class Devolucion
    {
        [Key]
        public int Id { get; set; }

        public Item? Item { get; set; }
		public int ItemId { get; set; }

        public Personal? Personal { get; set; }
		public int PersonalId { get; set; }

        public double Cantidad { get; set; }

        public DateTime FechaDevolucion { get; set; }

        public string Observaciones { get; set; }      

    }
}