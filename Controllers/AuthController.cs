using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrack.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(AuthService auth, SesionService sesiones) : ControllerBase
{
    private const string RutaCookie = "/api/v1/auth";

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<RespuestaApi<LoginRespuesta>>> Login(LoginSolicitud solicitud, CancellationToken cancelacion)
    {
        var respuesta = await auth.IniciarSesionAsync(solicitud, Ip(), cancelacion);
        var token = await sesiones.CrearAsync(respuesta.Usuario.UsuarioId, null, Ip(), Agente(), cancelacion);
        EscribirCookie(token);
        return new RespuestaApi<LoginRespuesta>(respuesta);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<RespuestaApi<LoginRespuesta>>> Renovar(CancellationToken cancelacion)
    {
        var cookie = Request.Cookies[SesionService.NombreCookie];
        if (string.IsNullOrEmpty(cookie))
        {
            return NoContent();
        }

        var resultado = await sesiones.RenovarAsync(cookie, Ip(), Agente(), cancelacion);
        switch (resultado.Estado)
        {
            case EstadoRenovacion.Reintentar:
                throw new ApiException(StatusCodes.Status409Conflict, "SESION_ROTADA",
                    "La sesión se acaba de renovar en otra pestaña. Intenta de nuevo.");
            case EstadoRenovacion.Invalida:
                BorrarCookie();
                return StatusCode(StatusCodes.Status401Unauthorized, RespuestaError.Crear(
                    CodigosError.NoAutenticado, "Tu sesión terminó. Vuelve a iniciar sesión.", HttpContext));
            default:
                EscribirCookie(resultado.Token!);
                return new RespuestaApi<LoginRespuesta>(auth.Respuesta(resultado.Usuario!));
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> CerrarSesion(CancellationToken cancelacion)
    {
        await sesiones.CerrarAsync(Request.Cookies[SesionService.NombreCookie], cancelacion);
        BorrarCookie();
        return NoContent();
    }

    [HttpGet("perfil")]
    [Authorize]
    public async Task<ActionResult<RespuestaApi<PerfilRespuesta>>> Perfil(CancellationToken cancelacion) =>
        new RespuestaApi<PerfilRespuesta>(await auth.ObtenerPerfilAsync(User.ObtenerUsuarioId(), cancelacion));

    [HttpPatch("contrasena")]
    [Authorize]
    public async Task<ActionResult<RespuestaApi<LoginRespuesta>>> CambiarContrasena(
        ContrasenaCambiarSolicitud solicitud, CancellationToken cancelacion)
    {
        var usuarioId = User.ObtenerUsuarioId();
        var respuesta = await auth.CambiarContrasenaAsync(usuarioId, solicitud, cancelacion);
        await sesiones.RevocarUsuarioAsync(usuarioId, cancelacion);
        EscribirCookie(await sesiones.CrearAsync(usuarioId, null, Ip(), Agente(), cancelacion));
        return new RespuestaApi<LoginRespuesta>(respuesta);
    }

    private void EscribirCookie(string token) =>
        Response.Cookies.Append(SesionService.NombreCookie, token, Opciones(DateTimeOffset.UtcNow + SesionService.Duracion));

    private void BorrarCookie() =>
        Response.Cookies.Delete(SesionService.NombreCookie, Opciones(null));

    private CookieOptions Opciones(DateTimeOffset? expira) => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = RutaCookie,
        Expires = expira,
        IsEssential = true,
    };

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? Agente() => Request.Headers.UserAgent.ToString() is { Length: > 0 } agente ? agente : null;
}
