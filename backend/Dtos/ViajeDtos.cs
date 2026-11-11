using EcoTrack.Models;

namespace EcoTrack.Dtos;

public sealed record ViajeGuardarSolicitud(
    Guid? VehiculoId,
    Guid? ChoferId,
    string? Origen,
    string? Destino,
    DateTimeOffset? SalidaEn,
    DateTimeOffset? LlegadaEn,
    decimal? DistanciaKm,
    string? Estado);

public sealed record ViajeEstadoSolicitud(string? Estado, DateTimeOffset? LlegadaEn);

public sealed record ViajeFiltro(
    DateOnly? Desde,
    DateOnly? Hasta,
    Guid? VehiculoId,
    Guid? ChoferId,
    string? Estado,
    Guid? CargaArchivoId,
    string? Q,
    int Page,
    int Limit);

public sealed record ConteosViaje(int Planificado, int EnCurso, int Completado, int Cancelado, int Todos);

public sealed record MetaViajes(
    int Page,
    int Limit,
    int Total,
    int TotalPages,
    decimal TotalKm,
    decimal TotalCo2Kg,
    ConteosViaje Conteos);

public sealed record ViajeRespuesta(
    Guid ViajeId,
    Guid VehiculoId,
    string Placa,
    Guid? ChoferId,
    string? Chofer,
    string Origen,
    string Destino,
    DateTimeOffset SalidaEn,
    DateTimeOffset? LlegadaEn,
    decimal DistanciaKm,
    decimal Co2Kg,
    EstadoViaje Estado,
    Guid? CargaArchivoId);

public sealed record VehiculoDelViaje(
    Guid VehiculoId,
    string Placa,
    TipoVehiculo TipoVehiculo,
    TipoCombustible TipoCombustible,
    EstadoVehiculo Estado);

public sealed record ChoferDelViaje(Guid ChoferId, string Nombres, string Apellidos, string NumeroLicencia, EstadoChofer Estado);

public sealed record FactorDelViaje(
    Guid FactorEmisionId,
    decimal FactorCo2Km,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    string? Fuente);

public sealed record CargaDelViaje(Guid CargaArchivoId, string? NombreArchivo, DateTimeOffset CreadoEn);

public sealed record ViajeDetalleRespuesta(
    ViajeRespuesta Viaje,
    VehiculoDelViaje Vehiculo,
    ChoferDelViaje? Chofer,
    FactorDelViaje Factor,
    CargaDelViaje? Carga,
    string? CreadoPor,
    DateTimeOffset CreadoEn);
