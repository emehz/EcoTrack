using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services;

public sealed class AuthService(AppDbContext db, TokenService tokens)
{
    private const string CredencialesInvalidas = "Correo o contraseña incorrectos.";
    private const string HashDeRelleno = "$2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW";

    public async Task<LoginRespuesta> IniciarSesionAsync(LoginSolicitud solicitud, CancellationToken cancelacion)
    {
        var correo = solicitud.Correo?.Trim().ToLowerInvariant();
        var contrasena = solicitud.Contrasena ?? string.Empty;

        var usuario = string.IsNullOrEmpty(correo)
            ? null
#pragma warning disable CA1304, CA1311, CA1862
            : await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Correo.ToLower() == correo, cancelacion);
#pragma warning restore CA1304, CA1311, CA1862

        var contrasenaCorrecta = BCrypt.Net.BCrypt.Verify(contrasena, usuario?.HashContrasena ?? HashDeRelleno);
        if (usuario is null || !contrasenaCorrecta || !usuario.Activo)
        {
            throw ApiException.NoAutenticado(CredencialesInvalidas);
        }

        var (token, expiraEn) = tokens.Emitir(usuario);
        return new LoginRespuesta(token, "Bearer", expiraEn, APerfil(usuario));
    }

    public async Task<PerfilRespuesta> ObtenerPerfilAsync(Guid usuarioId, CancellationToken cancelacion)
    {
        var usuario = await db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UsuarioId == usuarioId && u.Activo, cancelacion);

        return usuario is null
            ? throw ApiException.NoAutenticado("Tu cuenta ya no está activa.")
            : APerfil(usuario);
    }

    private static PerfilRespuesta APerfil(Usuario usuario) =>
        new(usuario.UsuarioId, usuario.Correo, usuario.Nombres, usuario.Apellidos, Nombres.DeRol(usuario.Rol), usuario.Activo, usuario.CreadoEn);
}
