using EcoTrack.Models;

namespace EcoTrack.Dtos;

public sealed record CargaGuardarSolicitud(
    Guid? VehiculoId,
    DateTimeOffset? CargadoEn,
    decimal? Litros,
    decimal? PrecioPorLitro,
    Guid? ViajeId);

public sealed record CargaFiltro(Guid? VehiculoId, DateOnly? Desde, DateOnly? Hasta, int Page, int Limit);

public sealed record MetaCargas(int Page, int Limit, int Total, int TotalPages, decimal TotalLitros, decimal TotalCosto);

public sealed record CargaRespuesta(
    Guid CargaCombustibleId,
    Guid VehiculoId,
    string Placa,
    TipoCombustible TipoCombustible,
    DateTimeOffset CargadoEn,
    decimal Litros,
    decimal? PrecioPorLitro,
    decimal? Costo,
    Guid? ViajeId,
    string? Viaje);
