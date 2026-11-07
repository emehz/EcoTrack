using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Controllers;

[ApiController]
[Route("api/v1")]
public class GeneralController(AppDbContext db, FactorEmisionService factores) : ControllerBase
{
    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<ActionResult<RespuestaApi<SaludRespuesta>>> Salud(CancellationToken cancelacion)
    {
        var conectada = await db.Database.CanConnectAsync(cancelacion);
        var respuesta = new RespuestaApi<SaludRespuesta>(new SaludRespuesta(conectada ? "ok" : "degradado", conectada));
        return conectada ? respuesta : StatusCode(StatusCodes.Status503ServiceUnavailable, respuesta);
    }

    [HttpGet("catalogos")]
    [Authorize]
    public ActionResult<RespuestaApi<CatalogosRespuesta>> ObtenerCatalogos() =>
        new RespuestaApi<CatalogosRespuesta>(Catalogos.Todos());

    [HttpGet("factores/vigente")]
    [Authorize]
    public async Task<ActionResult<RespuestaApi<FactorVigenteRespuesta>>> FactorVigente(
        [FromQuery(Name = "tipo_vehiculo")] string? tipoVehiculo,
        [FromQuery(Name = "tipo_combustible")] string? tipoCombustible,
        [FromQuery] DateOnly? fecha,
        CancellationToken cancelacion) =>
        new RespuestaApi<FactorVigenteRespuesta>(
            await factores.ObtenerVigenteAsync(tipoVehiculo, tipoCombustible, fecha, cancelacion));

    [HttpPost("factores")]
    [Authorize(Roles = Nombres.RolAdmin)]
    public async Task<ActionResult<RespuestaApi<FactorVigenteRespuesta>>> CrearFactor(
        FactorCrearSolicitud solicitud, CancellationToken cancelacion) =>
        StatusCode(
            StatusCodes.Status201Created,
            new RespuestaApi<FactorVigenteRespuesta>(await factores.CrearAsync(solicitud, User.ObtenerUsuarioId(), cancelacion)));
}
