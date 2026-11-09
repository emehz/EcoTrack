using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using EcoTrack.Services.Reportes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reportes")]
public class ReportesController(ReporteService reportes) : ControllerBase
{
    private const string RolesQueGeneran = Nombres.RolAdmin + "," + Nombres.RolOperador;

    [HttpPost]
    [Authorize(Roles = RolesQueGeneran)]
    public async Task<ActionResult<RespuestaApi<ReporteDetalleRespuesta>>> Generar(
        ReporteGenerarSolicitud solicitud, CancellationToken cancelacion)
    {
        var reporte = await reportes.GenerarAsync(solicitud, User.ObtenerUsuarioId(), cancelacion);
        return CreatedAtAction(nameof(Obtener), new { id = reporte.ReporteViajeId }, new RespuestaApi<ReporteDetalleRespuesta>(reporte));
    }

    [HttpGet]
    public async Task<ActionResult<RespuestaApi<IReadOnlyList<ReporteResumenRespuesta>>>> Listar(
        [FromQuery] string? estado,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken cancelacion,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var (datos, meta) = await reportes.ListarAsync(new ReporteFiltro(estado, desde, hasta, page, limit), cancelacion);
        return new RespuestaApi<IReadOnlyList<ReporteResumenRespuesta>>(datos, meta);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RespuestaApi<ReporteDetalleRespuesta>>> Obtener(Guid id, CancellationToken cancelacion) =>
        new RespuestaApi<ReporteDetalleRespuesta>(await reportes.ObtenerAsync(id, cancelacion));

    [HttpGet("{id:guid}/versiones/{numero:int}/filas")]
    public async Task<ActionResult<RespuestaApi<IReadOnlyList<FilaHuellaRespuesta>>>> Filas(
        Guid id,
        int numero,
        [FromQuery] string? estado,
        CancellationToken cancelacion,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50)
    {
        var (datos, meta) = await reportes.ListarFilasAsync(id, numero, new FilasFiltro(estado, page, limit), cancelacion);
        return new RespuestaApi<IReadOnlyList<FilaHuellaRespuesta>>(datos, meta);
    }

    [HttpGet("{id:guid}/versiones/{numero:int}/csv")]
    public async Task<IActionResult> Descargar(Guid id, int numero, CancellationToken cancelacion)
    {
        var (contenido, nombre) = await reportes.DescargarAsync(id, numero, cancelacion);
        return File(contenido, "text/csv; charset=utf-8", nombre);
    }

    [HttpPost("{id:guid}/versiones/{numero:int}/reprocesar")]
    [Authorize(Roles = RolesQueGeneran)]
    public async Task<ActionResult<RespuestaApi<VersionRespuesta>>> Reprocesar(Guid id, int numero, CancellationToken cancelacion) =>
        Accepted(new RespuestaApi<VersionRespuesta>(await reportes.ReprocesarAsync(id, numero, User.ObtenerUsuarioId(), cancelacion)));
}
