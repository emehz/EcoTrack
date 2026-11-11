using EcoTrack.Models;

namespace EcoTrack.Dtos;

public sealed record FactorFiltro(
    string? TipoVehiculo,
    string? TipoCombustible,
    string? Vigencia,
    DateOnly? Desde,
    DateOnly? Hasta,
    int Page,
    int Limit);

public sealed record FactorSustituirSolicitud(decimal? FactorCo2Km, DateOnly? VigenteDesde, string? Fuente);

public sealed record FactorRespuesta(
    Guid FactorEmisionId,
    TipoVehiculo TipoVehiculo,
    TipoCombustible TipoCombustible,
    decimal FactorCo2Km,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    string? Fuente,
    bool Vigente,
    int Viajes);

public sealed record FactorSustitucionRespuesta(FactorRespuesta Anterior, FactorRespuesta Nuevo);
