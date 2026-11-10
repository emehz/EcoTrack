using EcoTrack.Models;

namespace EcoTrack.Dtos;

public sealed record ChoferGuardarSolicitud(string? Nombres, string? Apellidos, string? NumeroLicencia, string? Telefono);

public sealed record ChoferEstadoSolicitud(string? Estado);

public sealed record ChoferFiltro(string? Q, string? Estado, string? Sort, string? Order, int Page, int Limit);

public sealed record ConteosChofer(int Activo, int Inactivo, int Suspendido, int Todos);

public sealed record MetaChoferes(int Page, int Limit, int Total, int TotalPages, ConteosChofer Conteos);

public sealed record ChoferRespuesta(
    Guid ChoferId,
    string Nombres,
    string Apellidos,
    string NumeroLicencia,
    string? Telefono,
    EstadoChofer Estado);

public sealed record ChoferDetalleRespuesta(
    ChoferRespuesta Chofer,
    int ViajesCompletados,
    decimal KmRecorridos,
    DateTimeOffset? UltimoViajeEn);

public sealed record ResumenViajesChofer(int Cantidad, decimal Km, DateTimeOffset? Ultimo);
