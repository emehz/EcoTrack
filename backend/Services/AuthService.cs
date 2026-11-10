using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services;

public sealed class AuthService(AppDbContext db, TokenService tokens, IntentosLogin intentos)
{
    private const string CredencialesInvalidas = "Correo o contraseña incorrectos.";
    private const string HashDeRelleno = "$2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW";

    public async Task<LoginRespuesta> IniciarSesionAsync(LoginSolicitud solicitud, string? ip, CancellationToken cancelacion)
    {
        var correo = solicitud.Correo?.Trim().ToLowerInvariant();
        var contrasena = solicitud.Contrasena ?? string.Empty;
        var clave = $"{correo}|{ip}";
        intentos.ExigirPermitido(clave);

        var usuario = string.IsNullOrEmpty(correo)
            ? null
#pragma warning disable CA1304, CA1311, CA1862
            : await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Correo.ToLower() == correo, cancelacion);
#pragma warning restore CA1304, CA1311, CA1862

        var contrasenaCorrecta = ContrasenaSegura.Coincide(contrasena, usuario?.HashContrasena ?? HashDeRelleno);
        if (usuario is null || !contrasenaCorrecta || !usuario.Activo)
        {
            intentos.RegistrarFallo(clave);
            throw ApiException.NoAutenticado(CredencialesInvalidas);
        }

        intentos.Limpiar(clave);
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

    public async Task CambiarContrasenaAsync(Guid usuarioId, ContrasenaCambiarSolicitud solicitud, CancellationToken cancelacion)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == usuarioId && u.Activo, cancelacion)
            ?? throw ApiException.NoAutenticado("Tu cuenta ya no está activa.");

        var errores = new List<ErrorDetalle>();
        if (string.IsNullOrEmpty(solicitud.ContrasenaActual)
            || !ContrasenaSegura.Coincide(solicitud.ContrasenaActual, usuario.HashContrasena))
        {
            errores.Add(new ErrorDetalle("contrasena_actual", "La contraseña actual no es correcta."));
        }

        ContrasenaSegura.Validar(solicitud.ContrasenaNueva, "contrasena_nueva", errores);
        if (errores.Count == 0 && solicitud.ContrasenaNueva == solicitud.ContrasenaActual)
        {
            errores.Add(new ErrorDetalle("contrasena_nueva", "La nueva contraseña debe ser distinta de la actual."));
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        usuario.HashContrasena = ContrasenaSegura.Hash(solicitud.ContrasenaNueva!);
        await db.GuardarComoAsync(usuarioId, cancelacion);
    }

    private static PerfilRespuesta APerfil(Usuario usuario) =>
        new(usuario.UsuarioId, usuario.Correo, usuario.Nombres, usuario.Apellidos, Nombres.DeRol(usuario.Rol), usuario.Activo, usuario.CreadoEn);
}
