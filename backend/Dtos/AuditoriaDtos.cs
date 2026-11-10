using EcoTrack.Models;

namespace EcoTrack.Dtos;

public sealed record AuditoriaIniciarSolicitud(string? Alcance, string? MotivoCiclo);

public sealed record HallazgoCrearSolicitud(
    int? FilaNumero, string? Campo, string? Severidad, string? Descripcion, string? ValorEncontrado, string? ValorEsperado);

public sealed record HallazgoEstadoSolicitud(string? Estado, string? Justificacion, int? VersionCorreccion);

public sealed record AuditoriaCerrarSolicitud(string? Resultado, string? Conclusion, int? VersionFinal);

public sealed record AuditoriaFiltro(string? Estado, string? Resultado, Guid? ReporteViajeId, int Page, int Limit);

public sealed record HallazgoRespuesta(
    Guid HallazgoAuditoriaId,
    OrigenHallazgo Origen,
    string? Control,
    SeveridadHallazgo Severidad,
    int? FilaNumero,
    string? Campo,
    string Descripcion,
    string? ValorEncontrado,
    string? ValorEsperado,
    EstadoHallazgo Estado,
    string? Justificacion,
    string? ResueltoPor,
    DateTimeOffset? ResueltoEn,
    string? DetectadoPor,
    DateTimeOffset DetectadoEn);

public sealed record VersionAuditadaRespuesta(int NumeroVersion, string HashSha256, EstadoCalculo EstadoCalculo, bool EsOficial);

public sealed record AuditoriaResumenRespuesta(
    Guid AuditoriaReporteId,
    Guid ReporteViajeId,
    string NombreArchivo,
    int VersionAuditada,
    string Auditor,
    EstadoAuditoria Estado,
    ResultadoAuditoria? Resultado,
    DateTimeOffset IniciadoEn,
    DateTimeOffset? CerradoEn,
    int HallazgosAbiertos,
    int HallazgosGravesAbiertos);

public sealed record AuditoriaDetalleRespuesta(
    Guid AuditoriaReporteId,
    Guid ReporteViajeId,
    string NombreArchivo,
    DateOnly PeriodoDesde,
    DateOnly PeriodoHasta,
    EstadoReporte EstadoReporte,
    VersionAuditadaRespuesta VersionAuditada,
    VersionAuditadaRespuesta? VersionFinal,
    string Auditor,
    EstadoAuditoria Estado,
    ResultadoAuditoria? Resultado,
    string? Alcance,
    string? Conclusion,
    string? MotivoCiclo,
    DateTimeOffset IniciadoEn,
    DateTimeOffset? CerradoEn,
    IReadOnlyList<HallazgoRespuesta> Hallazgos);
