using EcoTrack.Models;

namespace EcoTrack.Dtos;

public sealed record LoginSolicitud(string? Correo, string? Contrasena);

public sealed record PerfilRespuesta(
    Guid UsuarioId, string Correo, string Nombres, string Apellidos, string Rol, bool Activo, DateTimeOffset CreadoEn);

public sealed record LoginRespuesta(string Token, string Tipo, DateTimeOffset ExpiraEn, PerfilRespuesta Usuario);

public sealed record VehiculoCrearSolicitud(string? Placa, string? TipoVehiculo, string? TipoCombustible, string? Estado);

public sealed record VehiculoActualizarSolicitud(string? TipoVehiculo, string? TipoCombustible, string? Estado);

public sealed record VehiculoEstadoSolicitud(string? Estado);

public sealed record VehiculoFiltro(
    string? Q, string? Estado, string? TipoVehiculo, string? TipoCombustible, string? Sort, string? Order, int Page, int Limit);

public sealed record VehiculoRespuesta(
    Guid VehiculoId,
    string Placa,
    TipoVehiculo TipoVehiculo,
    TipoCombustible TipoCombustible,
    EstadoVehiculo Estado,
    decimal? FactorCo2Km,
    DateTimeOffset? UltimoViajeEn,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn);

public sealed record VehiculoDetalleRespuesta(
    VehiculoRespuesta Vehiculo,
    int ViajesCompletados,
    decimal KmRecorridos,
    decimal Co2AcumuladoKg,
    DateTimeOffset? UltimaCargaEn,
    decimal? UltimaCargaLitros);

public sealed record FactorVigenteRespuesta(
    Guid FactorEmisionId,
    TipoVehiculo TipoVehiculo,
    TipoCombustible TipoCombustible,
    decimal FactorCo2Km,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    string? Fuente);

public sealed record FactorCrearSolicitud(
    string? TipoVehiculo, string? TipoCombustible, decimal? FactorCo2Km, DateOnly? VigenteDesde, string? Fuente);

public sealed record OpcionCatalogo(string Valor, string Etiqueta);

public sealed record CatalogosRespuesta(
    IReadOnlyList<OpcionCatalogo> TiposVehiculo,
    IReadOnlyList<OpcionCatalogo> TiposCombustible,
    IReadOnlyList<OpcionCatalogo> EstadosVehiculo,
    IReadOnlyList<OpcionCatalogo> ClasesPlaca);

public sealed record SaludRespuesta(string Estado, bool BaseDeDatos);
