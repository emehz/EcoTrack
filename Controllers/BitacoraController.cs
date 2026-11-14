using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrack.Controllers;

[ApiController]
[Authorize(Roles = Nombres.RolAdmin + "," + Nombres.RolAuditor)]
[Route("api/v1/bitacora")]
public class BitacoraController(BitacoraService bitacora) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespuestaApi<IReadOnlyList<BitacoraRespuesta>>>> Listar(
        [FromQuery] string? entidad,
        [FromQuery(Name = "entidad_id")] Guid? entidadId,
        [FromQuery(Name = "usuario_id")] Guid? usuarioId,
        [FromQuery] string? accion,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken cancelacion,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var filtro = new BitacoraFiltro(entidad, entidadId, usuarioId, accion, desde, hasta, page, limit);
        var (datos, meta) = await bitacora.ListarAsync(filtro, cancelacion);
        return new RespuestaApi<IReadOnlyList<BitacoraRespuesta>>(datos, meta);
    }

    [HttpGet("usuarios")]
    public async Task<ActionResult<RespuestaApi<IReadOnlyList<UsuarioBitacora>>>> Usuarios(CancellationToken cancelacion) =>
        new RespuestaApi<IReadOnlyList<UsuarioBitacora>>(await bitacora.UsuariosAsync(cancelacion));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<RespuestaApi<BitacoraDetalleRespuesta>>> Obtener(long id, CancellationToken cancelacion) =>
        new RespuestaApi<BitacoraDetalleRespuesta>(await bitacora.ObtenerAsync(id, cancelacion));
}
