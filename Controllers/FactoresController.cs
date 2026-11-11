using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/factores")]
public class FactoresController(FactorHistorialService factores) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespuestaApi<IReadOnlyList<FactorRespuesta>>>> Listar(
        [FromQuery(Name = "tipo_vehiculo")] string? tipoVehiculo,
        [FromQuery(Name = "tipo_combustible")] string? tipoCombustible,
        [FromQuery] string? vigencia,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken cancelacion,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50)
    {
        var filtro = new FactorFiltro(tipoVehiculo, tipoCombustible, vigencia, desde, hasta, page, limit);
        var (datos, meta) = await factores.ListarAsync(filtro, cancelacion);
        return new RespuestaApi<IReadOnlyList<FactorRespuesta>>(datos, meta);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RespuestaApi<FactorRespuesta>>> Obtener(Guid id, CancellationToken cancelacion) =>
        new RespuestaApi<FactorRespuesta>(await factores.ObtenerAsync(id, cancelacion));

    [HttpPost("{id:guid}/sustituir")]
    [Authorize(Roles = Nombres.RolAdmin)]
    public async Task<ActionResult<RespuestaApi<FactorSustitucionRespuesta>>> Sustituir(
        Guid id, FactorSustituirSolicitud solicitud, CancellationToken cancelacion) =>
        StatusCode(
            StatusCodes.Status201Created,
            new RespuestaApi<FactorSustitucionRespuesta>(
                await factores.SustituirAsync(id, solicitud, User.ObtenerUsuarioId(), cancelacion)));
}
