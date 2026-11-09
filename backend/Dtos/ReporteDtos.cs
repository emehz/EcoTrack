using EcoTrack.Models;

namespace EcoTrack.Dtos;

public sealed record ReporteGenerarSolicitud(
    DateOnly? Desde,
    DateOnly? Hasta,
    Guid? VehiculoId,
    Guid? ChoferId,
    string? TipoVehiculo,
    string? TipoCombustible,
    bool? SoloCompletados);

public sealed record FiltrosReporte(
    Guid? VehiculoId, Guid? ChoferId, string? TipoVehiculo, string? TipoCombustible, bool SoloCompletados);

public sealed record ReporteFiltro(string? Estado, DateOnly? Desde, DateOnly? Hasta, int Page, int Limit);

public sealed record FilasFiltro(string? Estado, int Page, int Limit);

public sealed record HuellaResumen(
    int FilasProcesadas,
    int FilasValidas,
    int FilasConError,
    decimal DistanciaTotalKm,
    decimal Co2TotalKg,
    decimal Co2TotalToneladas,
    DateTimeOffset CalculadoEn);

public sealed record VersionRespuesta(
    int NumeroVersion,
    OrigenVersion Origen,
    string HashSha256,
    long TamanoBytes,
    int TotalFilas,
    EstadoCalculo EstadoCalculo,
    string? MensajeError,
    DateTimeOffset? CalculoIniciadoEn,
    DateTimeOffset? CalculoTerminadoEn,
    bool EsOficial,
    DateTimeOffset CreadoEn,
    HuellaResumen? Huella);

public sealed record ReporteResumenRespuesta(
    Guid ReporteViajeId,
    string NombreArchivo,
    DateOnly PeriodoDesde,
    DateOnly PeriodoHasta,
    EstadoReporte Estado,
    int TotalFilas,
    string GeneradoPor,
    DateTimeOffset GeneradoEn,
    int Versiones,
    int? VersionOficial,
    EstadoCalculo? EstadoCalculo,
    decimal? Co2TotalKg,
    decimal? DistanciaTotalKm);

public sealed record ReporteDetalleRespuesta(
    Guid ReporteViajeId,
    string NombreArchivo,
    DateOnly PeriodoDesde,
    DateOnly PeriodoHasta,
    FiltrosReporte Filtros,
    EstadoReporte Estado,
    string? MensajeError,
    int TotalFilas,
    string GeneradoPor,
    DateTimeOffset GeneradoEn,
    IReadOnlyList<VersionRespuesta> Versiones);

public sealed record FilaHuellaRespuesta(
    int FilaNumero,
    string? ViajeReferencia,
    string? VehiculoPlaca,
    string? TipoVehiculo,
    string? TipoCombustible,
    DateTimeOffset? SalidaEn,
    decimal? DistanciaKm,
    decimal? FactorCo2Km,
    decimal? Co2Kg,
    decimal? Co2KgOperativo,
    EstadoDetalleHuella Estado,
    string? MensajeError);
