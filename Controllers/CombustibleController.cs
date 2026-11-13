using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/combustible")]
public class CombustibleController(CombustibleService combustible) : ControllerBase
{
    private const string Escritura = Nombres.RolAdmin + "," + Nombres.RolOperador;

    [HttpGet]
    public async Task<ActionResult<RespuestaApi<IReadOnlyList<CargaRespuesta>>>> Listar(
        [FromQuery(Name = "vehiculo_id")] Guid? vehiculoId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken cancelacion,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var (datos, meta) = await combustible.ListarAsync(new CargaFiltro(vehiculoId, desde, hasta, page, limit), cancelacion);
        return new RespuestaApi<IReadOnlyList<CargaRespuesta>>(datos, meta);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RespuestaApi<CargaRespuesta>>> Obtener(Guid id, CancellationToken cancelacion) =>
        new RespuestaApi<CargaRespuesta>(await combustible.ObtenerAsync(id, cancelacion));

    [HttpPost]
    [Authorize(Roles = Escritura)]
    public async Task<ActionResult<RespuestaApi<CargaRespuesta>>> Crear(CargaGuardarSolicitud solicitud, CancellationToken cancelacion)
    {
        var creada = await combustible.CrearAsync(solicitud, User.ObtenerUsuarioId(), cancelacion);
        return CreatedAtAction(nameof(Obtener), new { id = creada.CargaCombustibleId }, new RespuestaApi<CargaRespuesta>(creada));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = Escritura)]
    public async Task<ActionResult<RespuestaApi<CargaRespuesta>>> Actualizar(
        Guid id, CargaGuardarSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<CargaRespuesta>(await combustible.ActualizarAsync(id, solicitud, User.ObtenerUsuarioId(), cancelacion));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Nombres.RolAdmin)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancelacion, [FromQuery] bool confirmar = false)
    {
        await combustible.EliminarAsync(id, confirmar, User.ObtenerUsuarioId(), cancelacion);
        return NoContent();
    }
}
