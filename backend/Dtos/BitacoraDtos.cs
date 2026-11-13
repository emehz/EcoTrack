using System.Text.Json;

namespace EcoTrack.Dtos;

public sealed record BitacoraFiltro(
    string? Entidad,
    Guid? EntidadId,
    Guid? UsuarioId,
    string? Accion,
    DateOnly? Desde,
    DateOnly? Hasta,
    int Page,
    int Limit);

public sealed record BitacoraRespuesta(
    long AuditoriaId,
    DateTimeOffset CreadoEn,
    string Accion,
    string Entidad,
    Guid? EntidadId,
    Guid? UsuarioId,
    string? Usuario,
    IReadOnlyList<string> Campos);

public sealed record CambioBitacora(string Campo, JsonElement? Antes, JsonElement? Despues);

public sealed record BitacoraDetalleRespuesta(BitacoraRespuesta Registro, IReadOnlyList<CambioBitacora> Cambios);

public sealed record UsuarioBitacora(Guid UsuarioId, string Nombre);
