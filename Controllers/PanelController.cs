using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/panel")]
public class PanelController(PanelService panel) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespuestaApi<PanelRespuesta>>> Obtener(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery(Name = "tipo_vehiculo")] string? tipoVehiculo,
        [FromQuery(Name = "tipo_combustible")] string? tipoCombustible,
        CancellationToken cancelacion,
        [FromQuery] int top = 5) =>
        new RespuestaApi<PanelRespuesta>(
            await panel.ObtenerAsync(new PanelFiltro(desde, hasta, tipoVehiculo, tipoCombustible, top), cancelacion));
}
