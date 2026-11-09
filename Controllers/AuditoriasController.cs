using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using EcoTrack.Services.Auditorias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrack.Controllers;

[ApiController]
[Authorize(Roles = Nombres.RolAdmin + "," + Nombres.RolAuditor)]
[Route("api/v1")]
public class AuditoriasController(AuditoriaReporteService auditorias) : ControllerBase
{
    [HttpPost("reportes/{reporteId:guid}/auditorias")]
    public async Task<ActionResult<RespuestaApi<AuditoriaDetalleRespuesta>>> Iniciar(
        Guid reporteId, AuditoriaIniciarSolicitud solicitud, CancellationToken cancelacion)
    {
        var auditoria = await auditorias.IniciarAsync(reporteId, solicitud, User.ObtenerUsuarioId(), EsAdmin, cancelacion);
        return CreatedAtAction(nameof(Obtener), new { id = auditoria.AuditoriaReporteId }, new RespuestaApi<AuditoriaDetalleRespuesta>(auditoria));
    }

    [HttpGet("auditorias")]
    public async Task<ActionResult<RespuestaApi<IReadOnlyList<AuditoriaResumenRespuesta>>>> Listar(
        [FromQuery] string? estado,
        [FromQuery] string? resultado,
        [FromQuery(Name = "reporte_viaje_id")] Guid? reporteViajeId,
        CancellationToken cancelacion,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var (datos, meta) = await auditorias.ListarAsync(new AuditoriaFiltro(estado, resultado, reporteViajeId, page, limit), cancelacion);
        return new RespuestaApi<IReadOnlyList<AuditoriaResumenRespuesta>>(datos, meta);
    }

    [HttpGet("auditorias/{id:guid}")]
    public async Task<ActionResult<RespuestaApi<AuditoriaDetalleRespuesta>>> Obtener(Guid id, CancellationToken cancelacion) =>
        new RespuestaApi<AuditoriaDetalleRespuesta>(await auditorias.ObtenerAsync(id, cancelacion));

    [HttpPost("auditorias/{id:guid}/hallazgos")]
    public async Task<ActionResult<RespuestaApi<HallazgoRespuesta>>> RegistrarHallazgo(
        Guid id, HallazgoCrearSolicitud solicitud, CancellationToken cancelacion) =>
        StatusCode(StatusCodes.Status201Created, new RespuestaApi<HallazgoRespuesta>(
            await auditorias.RegistrarHallazgoAsync(id, solicitud, User.ObtenerUsuarioId(), EsAdmin, cancelacion)));

    [HttpPatch("auditorias/{id:guid}/hallazgos/{hallazgoId:guid}")]
    public async Task<ActionResult<RespuestaApi<HallazgoRespuesta>>> CambiarEstadoHallazgo(
        Guid id, Guid hallazgoId, HallazgoEstadoSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<HallazgoRespuesta>(
            await auditorias.CambiarEstadoHallazgoAsync(id, hallazgoId, solicitud, User.ObtenerUsuarioId(), EsAdmin, cancelacion));

    [HttpPost("auditorias/{id:guid}/cerrar")]
    public async Task<ActionResult<RespuestaApi<AuditoriaDetalleRespuesta>>> Cerrar(
        Guid id, AuditoriaCerrarSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<AuditoriaDetalleRespuesta>(
            await auditorias.CerrarAsync(id, solicitud, User.ObtenerUsuarioId(), EsAdmin, cancelacion));

    private bool EsAdmin => User.IsInRole(Nombres.RolAdmin);
}
