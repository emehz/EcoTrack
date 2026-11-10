using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/choferes")]
public class ChoferesController(ChoferService choferes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespuestaApi<IReadOnlyList<ChoferRespuesta>>>> Listar(
        [FromQuery] string? q,
        [FromQuery] string? estado,
        [FromQuery] string? sort,
        [FromQuery] string? order,
        CancellationToken cancelacion,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var (datos, meta) = await choferes.ListarAsync(new ChoferFiltro(q, estado, sort, order, page, limit), cancelacion);
        return new RespuestaApi<IReadOnlyList<ChoferRespuesta>>(datos, meta);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RespuestaApi<ChoferDetalleRespuesta>>> Obtener(Guid id, CancellationToken cancelacion) =>
        new RespuestaApi<ChoferDetalleRespuesta>(await choferes.ObtenerAsync(id, cancelacion));

    [HttpPost]
    [Authorize(Roles = Nombres.RolAdmin)]
    public async Task<ActionResult<RespuestaApi<ChoferRespuesta>>> Crear(ChoferGuardarSolicitud solicitud, CancellationToken cancelacion)
    {
        var creado = await choferes.CrearAsync(solicitud, User.ObtenerUsuarioId(), cancelacion);
        return CreatedAtAction(nameof(Obtener), new { id = creado.ChoferId }, new RespuestaApi<ChoferRespuesta>(creado));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = Nombres.RolAdmin)]
    public async Task<ActionResult<RespuestaApi<ChoferRespuesta>>> Actualizar(
        Guid id, ChoferGuardarSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<ChoferRespuesta>(await choferes.ActualizarAsync(id, solicitud, User.ObtenerUsuarioId(), cancelacion));

    [HttpPatch("{id:guid}/estado")]
    [Authorize(Roles = Nombres.RolAdmin)]
    public async Task<ActionResult<RespuestaApi<ChoferRespuesta>>> CambiarEstado(
        Guid id, ChoferEstadoSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<ChoferRespuesta>(await choferes.CambiarEstadoAsync(id, solicitud, User.ObtenerUsuarioId(), cancelacion));
}
