using Microsoft.AspNetCore.Cryptography.KeyDerivation;
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

	public enum enRoles
	{
		Administrador = 1,
		Pañolero = 2,
	}
	public class Usuario
	{
		[Key]
		[Display(Name = "Código de Usuario")]
		public int Id { get; set; }
		[Required]
		public string Nombre { get; set; }
		[Required]
		public string Apellido { get; set; }

		[Required]
		public string UsuarioLogin { get; set; }

		[Required, DataType(DataType.Password)]
		public string Password { get; set; }
		public int Rol { get; set; }
        [Display(Name = "Rol")]
        public string RolNombre => Rol > 0 ? ((enRoles)Rol).ToString() : "";
		public bool ActivarLogin { get; set; } = false; 
        // Propiedades para control de primer inicio de sesión
        // Usamos int para compatibilidad con MySQL (0 = false, 1 = true)
        public int PrimeraVezLogin { get; set; } = 1;

		public override string ToString()
        {
            return $"{Apellido} {Nombre}";
        }

		public static IDictionary<int, string> ObtenerRoles()
        {
            SortedDictionary<int, string> roles = new SortedDictionary<int, string>();
            Type? tipoEnumRol = typeof(enRoles);
            foreach (var valor in Enum.GetValues(tipoEnumRol))
            {
                roles.Add((int)valor, Enum.GetName(tipoEnumRol, valor));
            }
            return roles;
        }

        public static string hashearClave(string clave){
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                        password: clave,
                        salt: System.Text.Encoding.ASCII.GetBytes("Gestion_Compras"),
                        prf: KeyDerivationPrf.HMACSHA1,
                        iterationCount: 10000,
                        numBytesRequested: 256 / 8
                    ));
            return hashed;
        }

	}
}