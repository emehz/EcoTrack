using System.Security.Cryptography;
using System.Text;
using EcoTrack.Data;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services;

public enum EstadoRenovacion
{
    Renovada,
    Invalida,
    Reintentar,
}

public sealed record ResultadoRenovacion(EstadoRenovacion Estado, Usuario? Usuario = null, string? Token = null);

public sealed class SesionService(AppDbContext db, TimeProvider reloj)
{
    public const string NombreCookie = "ecotrack_refresh";
    public static readonly TimeSpan Duracion = TimeSpan.FromDays(7);
    private static readonly TimeSpan Gracia = TimeSpan.FromSeconds(20);
    private const int LargoMaximoAgente = 200;

    public async Task<string> CrearAsync(Guid usuarioId, Guid? familiaId, string? ip, string? agente, CancellationToken cancelacion)
    {
        var ahora = reloj.GetUtcNow();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        db.Sesiones.Add(new Sesion
        {
            SesionId = Guid.NewGuid(),
            UsuarioId = usuarioId,
            FamiliaId = familiaId ?? Guid.NewGuid(),
            HashToken = Hash(token),
            CreadoEn = ahora,
            ExpiraEn = ahora + Duracion,
            Ip = ip,
            Agente = agente is { Length: > LargoMaximoAgente } ? agente[..LargoMaximoAgente] : agente,
        });
        await db.SaveChangesAsync(cancelacion);
        await db.Sesiones.Where(s => s.UsuarioId == usuarioId && s.ExpiraEn < ahora).ExecuteDeleteAsync(cancelacion);
        return token;
    }

    public async Task<ResultadoRenovacion> RenovarAsync(string? token, string? ip, string? agente, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ResultadoRenovacion(EstadoRenovacion.Invalida);
        }

        var hash = Hash(token);
        var sesion = await db.Sesiones.AsNoTracking().Include(s => s.Usuario).FirstOrDefaultAsync(s => s.HashToken == hash, cancelacion);
        var ahora = reloj.GetUtcNow();
        if (sesion is null || sesion.RevocadoEn is not null || sesion.ExpiraEn <= ahora)
        {
            return new ResultadoRenovacion(EstadoRenovacion.Invalida);
        }

        if (sesion.UsadoEn is { } usado)
        {
            if (ahora - usado <= Gracia)
            {
                return new ResultadoRenovacion(EstadoRenovacion.Reintentar);
            }

            await RevocarFamiliaAsync(sesion.FamiliaId, cancelacion);
            return new ResultadoRenovacion(EstadoRenovacion.Invalida);
        }

        if (sesion.Usuario is not { Activo: true } usuario)
        {
            await RevocarFamiliaAsync(sesion.FamiliaId, cancelacion);
            return new ResultadoRenovacion(EstadoRenovacion.Invalida);
        }

        var tomada = await db.Sesiones
            .Where(s => s.SesionId == sesion.SesionId && s.UsadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsadoEn, ahora), cancelacion);
        if (tomada == 0)
        {
            return new ResultadoRenovacion(EstadoRenovacion.Reintentar);
        }

        var nuevo = await CrearAsync(usuario.UsuarioId, sesion.FamiliaId, ip, agente, cancelacion);
        return new ResultadoRenovacion(EstadoRenovacion.Renovada, usuario, nuevo);
    }

    public async Task CerrarAsync(string? token, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var hash = Hash(token);
        var familia = await db.Sesiones.Where(s => s.HashToken == hash).Select(s => (Guid?)s.FamiliaId).FirstOrDefaultAsync(cancelacion);
        if (familia is { } id)
        {
            await RevocarFamiliaAsync(id, cancelacion);
        }
    }

    public Task<int> RevocarUsuarioAsync(Guid usuarioId, CancellationToken cancelacion)
    {
        var ahora = reloj.GetUtcNow();
        return db.Sesiones
            .Where(s => s.UsuarioId == usuarioId && s.RevocadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevocadoEn, ahora), cancelacion);
    }

    private Task<int> RevocarFamiliaAsync(Guid familiaId, CancellationToken cancelacion)
    {
        var ahora = reloj.GetUtcNow();
        return db.Sesiones
            .Where(s => s.FamiliaId == familiaId && s.RevocadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevocadoEn, ahora), cancelacion);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
