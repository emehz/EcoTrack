using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrack.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<RespuestaApi<LoginRespuesta>>> Login(LoginSolicitud solicitud, CancellationToken cancelacion) =>
        new RespuestaApi<LoginRespuesta>(await auth.IniciarSesionAsync(solicitud, cancelacion));

    [HttpGet("perfil")]
    [Authorize]
    public async Task<ActionResult<RespuestaApi<PerfilRespuesta>>> Perfil(CancellationToken cancelacion) =>
        new RespuestaApi<PerfilRespuesta>(await auth.ObtenerPerfilAsync(User.ObtenerUsuarioId(), cancelacion));
}
