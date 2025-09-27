using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gestion_Compras.Models
{
	public class DataContext : IdentityDbContext<IdentityUser>
	{
		public DataContext(DbContextOptions<DataContext> options) : base(options)
		{

		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			
			// Configuración explícita para el modelo Ingreso
			modelBuilder.Entity<Ingreso>(entity =>
			{
				entity.Property(e => e.PedidoId).HasColumnName("PedidoId");
			});

			// Configuración de la relación entre Item y UnidadDeMedida
			modelBuilder.Entity<Item>()
				.HasOne(i => i.UnidadDeMedida)
				.WithMany()
				.HasForeignKey(i => i.UnidadDeMedidaId)
				.OnDelete(DeleteBehavior.Restrict);
		}
		public DbSet<Familia> Familia { get; set; }

		public DbSet<SubFamilia> SubFamilia { get; set; }

        public DbSet<Item> Item { get; set; }

        public DbSet<UnidadDeMedida> UnidadDeMedida { get; set; }

		public DbSet<Personal> Personal { get; set; }

		public DbSet<Proveedor> Proveedor { get; set; }

		public DbSet<Pedido> Pedido { get; set; }

		public DbSet<Ingreso> Ingreso { get; set; }

		public DbSet<Salida> Salida { get; set; }

		public DbSet<Kardex> Kardex { get; set; }
		
		public DbSet<Usuario> Usuario { get; set; }

		public DbSet<Registro> Registro { get; set; }

		public DbSet<Ajuste> Ajuste { get; set; }

		public DbSet<Devolucion> Devolucion { get; set; }

	}
}