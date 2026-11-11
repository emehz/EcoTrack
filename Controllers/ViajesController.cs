using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/viajes")]
public class ViajesController(ViajeService viajes) : ControllerBase
{
    private const string Escritura = Nombres.RolAdmin + "," + Nombres.RolOperador;

    [HttpGet]
    public async Task<ActionResult<RespuestaApi<IReadOnlyList<ViajeRespuesta>>>> Listar(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery(Name = "vehiculo_id")] Guid? vehiculoId,
        [FromQuery(Name = "chofer_id")] Guid? choferId,
        [FromQuery] string? estado,
        [FromQuery(Name = "carga_archivo_id")] Guid? cargaArchivoId,
        [FromQuery] string? q,
        CancellationToken cancelacion,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var filtro = new ViajeFiltro(desde, hasta, vehiculoId, choferId, estado, cargaArchivoId, q, page, limit);
        var (datos, meta) = await viajes.ListarAsync(filtro, cancelacion);
        return new RespuestaApi<IReadOnlyList<ViajeRespuesta>>(datos, meta);
    }

    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery(Name = "vehiculo_id")] Guid? vehiculoId,
        [FromQuery(Name = "chofer_id")] Guid? choferId,
        [FromQuery] string? estado,
        [FromQuery(Name = "carga_archivo_id")] Guid? cargaArchivoId,
        [FromQuery] string? q,
        CancellationToken cancelacion)
    {
        var filtro = new ViajeFiltro(desde, hasta, vehiculoId, choferId, estado, cargaArchivoId, q, 1, ViajeService.LimiteExportacion);
        var (contenido, nombre) = await viajes.ExportarAsync(filtro, cancelacion);
        return File(contenido, "text/csv; charset=utf-8", nombre);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RespuestaApi<ViajeDetalleRespuesta>>> Obtener(Guid id, CancellationToken cancelacion) =>
        new RespuestaApi<ViajeDetalleRespuesta>(await viajes.ObtenerAsync(id, cancelacion));

    [HttpPost]
    [Authorize(Roles = Escritura)]
    public async Task<ActionResult<RespuestaApi<ViajeDetalleRespuesta>>> Crear(ViajeGuardarSolicitud solicitud, CancellationToken cancelacion)
    {
        var creado = await viajes.CrearAsync(solicitud, User.ObtenerUsuarioId(), cancelacion);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Viaje.ViajeId }, new RespuestaApi<ViajeDetalleRespuesta>(creado));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = Escritura)]
    public async Task<ActionResult<RespuestaApi<ViajeDetalleRespuesta>>> Actualizar(
        Guid id, ViajeGuardarSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<ViajeDetalleRespuesta>(await viajes.ActualizarAsync(id, solicitud, User.ObtenerUsuarioId(), cancelacion));

    [HttpPatch("{id:guid}/estado")]
    [Authorize(Roles = Escritura)]
    public async Task<ActionResult<RespuestaApi<ViajeDetalleRespuesta>>> CambiarEstado(
        Guid id, ViajeEstadoSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<ViajeDetalleRespuesta>(await viajes.CambiarEstadoAsync(
            id, solicitud, User.ObtenerUsuarioId(), User.IsInRole(Nombres.RolAdmin), cancelacion));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Nombres.RolAdmin)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancelacion, [FromQuery] bool confirmar = false)
    {
        await viajes.EliminarAsync(id, confirmar, User.ObtenerUsuarioId(), cancelacion);
        return NoContent();
    }
}
