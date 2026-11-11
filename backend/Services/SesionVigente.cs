using System.Security.Claims;
using EcoTrack.Data;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EcoTrack.Services;

public sealed class SesionVigente(IMemoryCache cache)
{
    private static readonly TimeSpan Duracion = TimeSpan.FromSeconds(30);

    private sealed record EstadoCuenta(bool Activo, RolUsuario Rol, string Sello);

    public async Task<bool> EsValidaAsync(ClaimsPrincipal principal, AppDbContext db, CancellationToken cancelacion)
    {
        if (!Guid.TryParse(principal.FindFirstValue(TokenService.ClaimSujeto), out var usuarioId))
        {
            return false;
        }

        var estado = await cache.GetOrCreateAsync(Clave(usuarioId), async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = Duracion;
            var usuario = await db.Usuarios.AsNoTracking()
                .Where(u => u.UsuarioId == usuarioId)
                .Select(u => new { u.Activo, u.Rol, u.HashContrasena })
                .FirstOrDefaultAsync(cancelacion);
            return usuario is null ? null : new EstadoCuenta(usuario.Activo, usuario.Rol, TokenService.Sello(usuario.HashContrasena));
        });

        return estado is { Activo: true }
            && Nombres.DeRol(estado.Rol) == principal.FindFirstValue(TokenService.ClaimRol)
            && estado.Sello == principal.FindFirstValue(TokenService.ClaimSello);
    }

    public void Olvidar(Guid usuarioId) => cache.Remove(Clave(usuarioId));

    private static string Clave(Guid usuarioId) => $"sesion:{usuarioId}";
}
