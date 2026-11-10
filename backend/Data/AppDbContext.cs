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
    public DbSet<Chofer> Choferes => Set<Chofer>();
    public DbSet<Viaje> Viajes => Set<Viaje>();
    public DbSet<CargaCombustible> CargasCombustible => Set<CargaCombustible>();
    public DbSet<ReporteViaje> ReportesViaje => Set<ReporteViaje>();
    public DbSet<ReporteVersion> ReporteVersiones => Set<ReporteVersion>();
    public DbSet<HuellaReporte> HuellasReporte => Set<HuellaReporte>();
    public DbSet<HuellaReporteDetalle> HuellasReporteDetalle => Set<HuellaReporteDetalle>();

    public DbSet<AuditoriaReporte> AuditoriasReporte => Set<AuditoriaReporte>();
    public DbSet<HallazgoAuditoria> HallazgosAuditoria => Set<HallazgoAuditoria>();

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

        modelBuilder.Entity<Chofer>(entidad =>
        {
            entidad.ToTable("chofer");
            entidad.HasKey(c => c.ChoferId);
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

        modelBuilder.Entity<ReporteViaje>(entidad =>
        {
            entidad.ToTable("reporte_viaje");
            entidad.HasKey(r => r.ReporteViajeId);
            entidad.Property(r => r.Filtros).HasColumnType("jsonb");
            entidad.HasOne(r => r.GeneradoPor).WithMany().HasForeignKey(r => r.GeneradoPorUsuarioId);
            entidad.HasMany(r => r.Versiones).WithOne(v => v.Reporte).HasForeignKey(v => v.ReporteViajeId);
        });

        modelBuilder.Entity<ReporteVersion>(entidad =>
        {
            entidad.ToTable("reporte_version");
            entidad.HasKey(v => v.ReporteVersionId);
            entidad.HasOne(v => v.Huella).WithOne().HasForeignKey<HuellaReporte>(h => h.ReporteVersionId);
        });

        modelBuilder.Entity<HuellaReporte>(entidad =>
        {
            entidad.ToTable("huella_reporte");
            entidad.HasKey(h => h.HuellaReporteId);
            entidad.HasMany(h => h.Detalle).WithOne().HasForeignKey(d => d.HuellaReporteId);
        });

        modelBuilder.Entity<HuellaReporteDetalle>(entidad =>
        {
            entidad.ToTable("huella_reporte_detalle");
            entidad.HasKey(d => d.HuellaReporteDetalleId);
            entidad.Property(d => d.HuellaReporteDetalleId).UseIdentityAlwaysColumn();
        });

        modelBuilder.Entity<AuditoriaReporte>(entidad =>
        {
            entidad.ToTable("auditoria_reporte");
            entidad.HasKey(a => a.AuditoriaReporteId);
            entidad.HasOne(a => a.Reporte).WithMany().HasForeignKey(a => a.ReporteViajeId);
            entidad.HasOne(a => a.VersionAuditada).WithMany().HasForeignKey(a => a.VersionAuditadaId);
            entidad.HasOne(a => a.VersionFinal).WithMany().HasForeignKey(a => a.VersionFinalId);
            entidad.HasOne(a => a.Auditor).WithMany().HasForeignKey(a => a.AuditorUsuarioId);
            entidad.HasMany(a => a.Hallazgos).WithOne().HasForeignKey(h => h.AuditoriaReporteId);
        });

        modelBuilder.Entity<HallazgoAuditoria>(entidad =>
        {
            entidad.ToTable("hallazgo_auditoria");
            entidad.HasKey(h => h.HallazgoAuditoriaId);
            entidad.HasOne(h => h.ResueltoPor).WithMany().HasForeignKey(h => h.ResueltoPorUsuarioId);
            entidad.HasOne(h => h.DetectadoPor).WithMany().HasForeignKey(h => h.DetectadoPorUsuarioId);
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
