using System.Globalization;
using System.Text.Json;
using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services;

public sealed class BitacoraService(AppDbContext db)
{
    public const int LimiteMaximo = 100;
    private static readonly TimeSpan HoraElSalvador = TimeSpan.FromHours(-6);

    public static readonly IReadOnlyList<string> Entidades =
    [
        "usuario", "chofer", "vehiculo", "viaje", "carga_combustible", "factor_emision",
        "reporte_viaje", "auditoria_reporte", "hallazgo_auditoria",
    ];

    private static readonly Dictionary<string, string> Acciones = new()
    {
        ["crear"] = "INSERT",
        ["editar"] = "UPDATE",
        ["eliminar"] = "DELETE",
    };

    private static readonly HashSet<string> CamposOmitidos = ["actualizado_en"];

    public async Task<(IReadOnlyList<BitacoraRespuesta> Datos, MetaPaginacion Meta)> ListarAsync(
        BitacoraFiltro filtro, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        if (filtro.Page < 1)
        {
            errores.Add(new ErrorDetalle("page", "La página empieza en 1."));
        }

        if (filtro.Limit is < 1 or > LimiteMaximo)
        {
            errores.Add(new ErrorDetalle("limit", string.Create(CultureInfo.InvariantCulture, $"El límite debe estar entre 1 y {LimiteMaximo}.")));
        }

        var entidad = filtro.Entidad?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(entidad) && !Entidades.Contains(entidad))
        {
            errores.Add(new ErrorDetalle("entidad", "La entidad no es válida."));
        }

        string? operacion = null;
        if (!string.IsNullOrWhiteSpace(filtro.Accion) && !Acciones.TryGetValue(filtro.Accion.Trim().ToLowerInvariant(), out operacion))
        {
            errores.Add(new ErrorDetalle("accion", "Usa crear, editar o eliminar."));
        }

        if (filtro.Desde is { } d && filtro.Hasta is { } h && d > h)
        {
            errores.Add(new ErrorDetalle("hasta", "La fecha final no puede ser anterior a la inicial."));
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var consulta = db.Bitacora.AsNoTracking();
        if (!string.IsNullOrEmpty(entidad))
        {
            consulta = consulta.Where(r => r.Entidad == entidad);
        }

        if (filtro.EntidadId is { } entidadId)
        {
            consulta = consulta.Where(r => r.EntidadId == entidadId);
        }

        if (filtro.UsuarioId is { } usuarioId)
        {
            consulta = consulta.Where(r => r.UsuarioId == usuarioId);
        }

        if (operacion is not null)
        {
            consulta = consulta.Where(r => r.Accion == operacion);
        }

        if (filtro.Desde is { } desde)
        {
            var inicio = InicioDelDia(desde);
            consulta = consulta.Where(r => r.CreadoEn >= inicio);
        }

        if (filtro.Hasta is { } hasta)
        {
            var fin = InicioDelDia(hasta.AddDays(1));
            consulta = consulta.Where(r => r.CreadoEn < fin);
        }

        var total = await consulta.CountAsync(cancelacion);
        var registros = await consulta
            .Include(r => r.Usuario)
            .OrderByDescending(r => r.CreadoEn).ThenByDescending(r => r.AuditoriaId)
            .Skip((filtro.Page - 1) * filtro.Limit)
            .Take(filtro.Limit)
            .ToListAsync(cancelacion);

        var datos = registros.Select(r => ARespuesta(r, Comparar(r))).ToList();
        return (datos, new MetaPaginacion(filtro.Page, filtro.Limit, total, (int)Math.Ceiling(total / (double)filtro.Limit)));
    }

    public async Task<BitacoraDetalleRespuesta> ObtenerAsync(long id, CancellationToken cancelacion)
    {
        var registro = await db.Bitacora.AsNoTracking()
            .Include(r => r.Usuario)
            .FirstOrDefaultAsync(r => r.AuditoriaId == id, cancelacion)
            ?? throw ApiException.NoEncontrado("El registro de la bitácora no existe.");

        var cambios = Comparar(registro);
        return new BitacoraDetalleRespuesta(ARespuesta(registro, cambios), cambios);
    }

    public async Task<IReadOnlyList<UsuarioBitacora>> UsuariosAsync(CancellationToken cancelacion) =>
        await db.Usuarios.AsNoTracking()
            .Where(u => db.Bitacora.Any(r => r.UsuarioId == u.UsuarioId))
            .OrderBy(u => u.Nombres).ThenBy(u => u.Apellidos)
            .Select(u => new UsuarioBitacora(u.UsuarioId, u.Nombres + " " + u.Apellidos))
            .ToListAsync(cancelacion);

    private static List<CambioBitacora> Comparar(RegistroBitacora registro)
    {
        var antes = Leer(registro.DatosAnteriores);
        var despues = Leer(registro.DatosNuevos);
        var campos = antes.Keys.Union(despues.Keys).Where(c => !CamposOmitidos.Contains(c)).OrderBy(c => c, StringComparer.Ordinal);

        var cambios = new List<CambioBitacora>();
        foreach (var campo in campos)
        {
            var tieneAntes = antes.TryGetValue(campo, out var valorAntes);
            var tieneDespues = despues.TryGetValue(campo, out var valorDespues);
            var igual = tieneAntes && tieneDespues && JsonElement.DeepEquals(valorAntes, valorDespues);
            if (!igual)
            {
                cambios.Add(new CambioBitacora(campo, tieneAntes ? valorAntes : null, tieneDespues ? valorDespues : null));
            }
        }

        return cambios;
    }

    private static Dictionary<string, JsonElement> Leer(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        using var documento = JsonDocument.Parse(json);
        return documento.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    private static BitacoraRespuesta ARespuesta(RegistroBitacora r, List<CambioBitacora> cambios) =>
        new(
            r.AuditoriaId,
            r.CreadoEn,
            Acciones.First(a => a.Value == r.Accion).Key,
            r.Entidad,
            r.EntidadId,
            r.UsuarioId,
            r.Usuario is { } u ? $"{u.Nombres} {u.Apellidos}" : null,
            cambios.Select(c => c.Campo).ToList());

    private static DateTimeOffset InicioDelDia(DateOnly fecha) =>
        new DateTimeOffset(fecha.ToDateTime(TimeOnly.MinValue), HoraElSalvador).ToUniversalTime();
}
