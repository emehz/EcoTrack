using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using EcoTrack.Models;
using Microsoft.IdentityModel.Tokens;

namespace EcoTrack.Services;

public sealed record JwtOpciones(string Secreto, string Emisor, string Audiencia, int MinutosDeVida)
{
    private const int BytesMinimos = 32;
    private const int MinutosMaximos = 60;

    public static JwtOpciones Desde(IConfiguration configuracion)
    {
        var secreto = configuracion["JWT_SECRET"] ?? throw new InvalidOperationException("Falta JWT_SECRET");
        if (Encoding.UTF8.GetByteCount(secreto) < BytesMinimos)
        {
            throw new InvalidOperationException("JWT_SECRET debe tener al menos 32 bytes");
        }

        return new JwtOpciones(
            secreto,
            configuracion["JWT_ISSUER"] ?? throw new InvalidOperationException("Falta JWT_ISSUER"),
            configuracion["JWT_AUDIENCE"] ?? throw new InvalidOperationException("Falta JWT_AUDIENCE"),
            MinutosMaximos);
    }
}

public sealed class TokenService(JwtOpciones opciones)
{
    public const string ClaimSujeto = JwtRegisteredClaimNames.Sub;
    public const string ClaimRol = "rol";
    public const string ClaimSello = "sello";

    public static string Sello(string hashContrasena) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashContrasena)))[..16];

    public (string Token, DateTimeOffset ExpiraEn) Emitir(Usuario usuario)
    {
        var expiraEn = DateTimeOffset.UtcNow.AddMinutes(opciones.MinutosDeVida);
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.Secreto)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: opciones.Emisor,
            audience: opciones.Audiencia,
            claims:
            [
                new Claim(ClaimSujeto, usuario.UsuarioId.ToString()),
                new Claim(ClaimRol, Nombres.DeRol(usuario.Rol)),
                new Claim(ClaimSello, Sello(usuario.HashContrasena)),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            expires: expiraEn.UtcDateTime,
            signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEn);
    }
}

public static class Nombres
{
    public const string RolAdmin = "admin";
    public const string RolOperador = "operador";

    public const string RolAuditor = "auditor";
    public static string DeRol(RolUsuario rol) => rol.ToString().ToLowerInvariant();
}
