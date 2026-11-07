using System.Text.RegularExpressions;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Data;

public partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public const string Esquema = "ecotrack";

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<FactorEmision> FactoresEmision => Set<FactorEmision>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<Viaje> Viajes => Set<Viaje>();
    public DbSet<CargaCombustible> CargasCombustible => Set<CargaCombustible>();

    public async Task GuardarComoAsync(Guid usuarioId, CancellationToken cancelacion)
    {
        await using var transaccion = await Database.BeginTransactionAsync(cancelacion);
        await Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('ecotrack.usuario_id', {usuarioId.ToString()}, true)", cancelacion);
        await SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);

        modelBuilder.Entity<Usuario>(entidad =>
        {
            entidad.ToTable("usuario");
            entidad.HasKey(u => u.UsuarioId);
        });

        modelBuilder.Entity<FactorEmision>(entidad =>
        {
            entidad.ToTable("factor_emision");
            entidad.HasKey(f => f.FactorEmisionId);
        });

        modelBuilder.Entity<Vehiculo>(entidad =>
        {
            entidad.ToTable("vehiculo", tabla =>
            {
                tabla.HasTrigger("trg_vehiculo_actualizado");
                tabla.HasTrigger("trg_vehiculo_validar_factor");
                tabla.HasTrigger("trg_vehiculo_auditoria");
            });
            entidad.HasKey(v => v.VehiculoId);
            entidad.Property(v => v.VehiculoId).HasDefaultValueSql("gen_random_uuid()");
            entidad.Property(v => v.CreadoEn).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            entidad.Property(v => v.ActualizadoEn).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        });

        modelBuilder.Entity<Viaje>(entidad =>
        {
            entidad.ToTable("viaje");
            entidad.HasKey(v => v.ViajeId);
            entidad.HasOne(v => v.Vehiculo).WithMany().HasForeignKey(v => v.VehiculoId);
        });

        modelBuilder.Entity<CargaCombustible>(entidad =>
        {
            entidad.ToTable("carga_combustible");
            entidad.HasKey(c => c.CargaCombustibleId);
            entidad.HasOne(c => c.Vehiculo).WithMany().HasForeignKey(c => c.VehiculoId);
        });

        foreach (var tipo in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var propiedad in tipo.GetProperties())
            {
                propiedad.SetColumnName(ASnakeCase(propiedad.Name));
            }
        }
    }

    private static string ASnakeCase(string nombre) =>
        InicioDePalabra().Replace(nombre, "_$1").ToLowerInvariant();

    [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
    private static partial Regex InicioDePalabra();
}
