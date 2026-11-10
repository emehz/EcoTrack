using EcoTrack.Models;

namespace EcoTrack.Dtos;

public sealed record UsuarioCrearSolicitud(string? Correo, string? Nombres, string? Apellidos, string? Rol, string? Contrasena);

public sealed record UsuarioActualizarSolicitud(string? Nombres, string? Apellidos, string? Rol);

public sealed record UsuarioEstadoSolicitud(bool? Activo);

public sealed record ContrasenaRestablecerSolicitud(string? ContrasenaNueva);

public sealed record ContrasenaCambiarSolicitud(string? ContrasenaActual, string? ContrasenaNueva);

public sealed record UsuarioFiltro(string? Q, string? Rol, string? Estado, int Page, int Limit);

public sealed record UsuarioRespuesta(
    Guid UsuarioId,
    string Correo,
    string Nombres,
    string Apellidos,
    RolUsuario Rol,
    bool Activo,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn);
