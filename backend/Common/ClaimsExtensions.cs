using System.Security.Claims;
using EcoTrack.Services;

namespace EcoTrack.Common;

public static class ClaimsExtensions
{
    public static Guid ObtenerUsuarioId(this ClaimsPrincipal usuario) =>
        Guid.TryParse(usuario.FindFirstValue(TokenService.ClaimSujeto), out var id)
            ? id
            : throw ApiException.NoAutenticado("Debes iniciar sesión.");
}
