using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrack.Controllers;

[ApiController]
[Authorize(Roles = Nombres.RolAdmin)]
[Route("api/v1/usuarios")]
public class UsuariosController(UsuarioService usuarios) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespuestaApi<IReadOnlyList<UsuarioRespuesta>>>> Listar(
        [FromQuery] string? q,
        [FromQuery] string? rol,
        [FromQuery] string? estado,
        CancellationToken cancelacion,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var (datos, meta) = await usuarios.ListarAsync(new UsuarioFiltro(q, rol, estado, page, limit), cancelacion);
        return new RespuestaApi<IReadOnlyList<UsuarioRespuesta>>(datos, meta);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RespuestaApi<UsuarioRespuesta>>> Obtener(Guid id, CancellationToken cancelacion) =>
        new RespuestaApi<UsuarioRespuesta>(await usuarios.ObtenerAsync(id, cancelacion));

    [HttpPost]
    public async Task<ActionResult<RespuestaApi<UsuarioRespuesta>>> Crear(UsuarioCrearSolicitud solicitud, CancellationToken cancelacion)
    {
        var creado = await usuarios.CrearAsync(solicitud, User.ObtenerUsuarioId(), cancelacion);
        return CreatedAtAction(nameof(Obtener), new { id = creado.UsuarioId }, new RespuestaApi<UsuarioRespuesta>(creado));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<RespuestaApi<UsuarioRespuesta>>> Actualizar(
        Guid id, UsuarioActualizarSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<UsuarioRespuesta>(await usuarios.ActualizarAsync(id, solicitud, User.ObtenerUsuarioId(), cancelacion));

    [HttpPatch("{id:guid}/estado")]
    public async Task<ActionResult<RespuestaApi<UsuarioRespuesta>>> CambiarEstado(
        Guid id, UsuarioEstadoSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<UsuarioRespuesta>(await usuarios.CambiarEstadoAsync(id, solicitud, User.ObtenerUsuarioId(), cancelacion));

    [HttpPost("{id:guid}/contrasena")]
    public async Task<IActionResult> RestablecerContrasena(
        Guid id, ContrasenaRestablecerSolicitud solicitud, CancellationToken cancelacion)
    {
        await usuarios.RestablecerContrasenaAsync(id, solicitud, User.ObtenerUsuarioId(), cancelacion);
        return NoContent();
    }
}
