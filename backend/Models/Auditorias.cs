namespace EcoTrack.Models;

public enum EstadoAuditoria
{
    EnRevision,
    Cerrada,
}

public enum ResultadoAuditoria
{
    Aprobado,
    AprobadoConAjustes,
    Rechazado,
}

public enum SeveridadHallazgo
{
    Critica,
    Alta,
    Media,
    Baja,
    Observacion,
}

public enum EstadoHallazgo
{
    Abierto,
    Corregido,
    RiesgoAceptado,
    Descartado,
}

public enum OrigenHallazgo
{
    Automatico,
    Manual,
}

public class AuditoriaReporte
{
    public Guid AuditoriaReporteId { get; set; }
    public Guid ReporteViajeId { get; set; }
    public ReporteViaje? Reporte { get; set; }
    public Guid VersionAuditadaId { get; set; }
    public ReporteVersion? VersionAuditada { get; set; }
    public Guid? VersionFinalId { get; set; }
    public ReporteVersion? VersionFinal { get; set; }
    public Guid AuditorUsuarioId { get; set; }
    public Usuario? Auditor { get; set; }
    public EstadoAuditoria Estado { get; set; }
    public ResultadoAuditoria? Resultado { get; set; }
    public string? Alcance { get; set; }
    public string? Conclusion { get; set; }
    public string? MotivoCiclo { get; set; }
    public DateTimeOffset IniciadoEn { get; set; }
    public DateTimeOffset? CerradoEn { get; set; }
    public List<HallazgoAuditoria> Hallazgos { get; } = [];
}

public class HallazgoAuditoria
{
    public Guid HallazgoAuditoriaId { get; set; }
    public Guid AuditoriaReporteId { get; set; }
    public Guid VersionOrigenId { get; set; }
    public OrigenHallazgo Origen { get; set; }
    public string? Control { get; set; }
    public SeveridadHallazgo Severidad { get; set; }
    public int? FilaNumero { get; set; }
    public string? Campo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? ValorEncontrado { get; set; }
    public string? ValorEsperado { get; set; }
    public EstadoHallazgo Estado { get; set; }
    public string? ValorCorregido { get; set; }
    public Guid? VersionCorreccionId { get; set; }
    public string? Justificacion { get; set; }
    public Guid? ResueltoPorUsuarioId { get; set; }
    public Usuario? ResueltoPor { get; set; }
    public DateTimeOffset? ResueltoEn { get; set; }
    public Guid? DetectadoPorUsuarioId { get; set; }
    public Usuario? DetectadoPor { get; set; }
    public DateTimeOffset DetectadoEn { get; set; }
}
