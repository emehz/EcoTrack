using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace EcoTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/vehiculos")]
public class VehiculosController(VehiculoService vehiculos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespuestaApi<IReadOnlyList<VehiculoRespuesta>>>> Listar(
        [FromQuery] string? q,
        [FromQuery] string? estado,
        [FromQuery(Name = "tipo_vehiculo")] string? tipoVehiculo,
        [FromQuery(Name = "tipo_combustible")] string? tipoCombustible,
        [FromQuery] string? sort,
        [FromQuery] string? order,
        CancellationToken cancelacion,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var filtro = new VehiculoFiltro(q, estado, tipoVehiculo, tipoCombustible, sort, order, page, limit);
        var (datos, meta) = await vehiculos.ListarAsync(filtro, cancelacion);
        return new RespuestaApi<IReadOnlyList<VehiculoRespuesta>>(datos, meta, Enlaces(meta));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RespuestaApi<VehiculoDetalleRespuesta>>> Obtener(Guid id, CancellationToken cancelacion) =>
        new RespuestaApi<VehiculoDetalleRespuesta>(await vehiculos.ObtenerAsync(id, cancelacion));

    [HttpPost]
    [Authorize(Roles = Nombres.RolAdmin)]
    public async Task<ActionResult<RespuestaApi<VehiculoRespuesta>>> Crear(VehiculoCrearSolicitud solicitud, CancellationToken cancelacion)
    {
        var creado = await vehiculos.CrearAsync(solicitud, User.ObtenerUsuarioId(), cancelacion);
        return CreatedAtAction(nameof(Obtener), new { id = creado.VehiculoId }, new RespuestaApi<VehiculoRespuesta>(creado));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = Nombres.RolAdmin)]
    public async Task<ActionResult<RespuestaApi<VehiculoRespuesta>>> Actualizar(
        Guid id, VehiculoActualizarSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<VehiculoRespuesta>(await vehiculos.ActualizarAsync(id, solicitud, User.ObtenerUsuarioId(), cancelacion));

    [HttpPatch("{id:guid}/estado")]
    [Authorize(Roles = Nombres.RolAdmin)]
    public async Task<ActionResult<RespuestaApi<VehiculoRespuesta>>> DarDeBaja(
        Guid id, VehiculoEstadoSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<VehiculoRespuesta>(await vehiculos.DarDeBajaAsync(id, solicitud, User.ObtenerUsuarioId(), cancelacion));

    private EnlacesPaginacion Enlaces(MetaPaginacion meta)
    {
        var self = Request.GetEncodedUrl();
        var siguiente = meta.Page < meta.TotalPages
            ? QueryHelpers.AddQueryString(
                $"{Request.Scheme}://{Request.Host}{Request.Path}",
                Request.Query
                    .Where(p => p.Key != "page")
                    .Select(p => KeyValuePair.Create(p.Key, p.Value))
                    .Append(KeyValuePair.Create("page", new Microsoft.Extensions.Primitives.StringValues((meta.Page + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)))))
            : null;
        return new EnlacesPaginacion(self, siguiente);
    }
}
