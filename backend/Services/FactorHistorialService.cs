using System.Globalization;
using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services;

public sealed class FactorHistorialService(AppDbContext db)
{
    public const int LimiteMaximo = 100;
    private const decimal FactorMaximo = 999999.999999m;
    private const int LargoMaximoFuente = 200;

    public async Task<(IReadOnlyList<FactorRespuesta> Datos, MetaPaginacion Meta)> ListarAsync(
        FactorFiltro filtro, CancellationToken cancelacion)
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

        var tipo = string.IsNullOrWhiteSpace(filtro.TipoVehiculo) ? null : Catalogos.LeerTipo(filtro.TipoVehiculo, "tipo_vehiculo", errores);
        var combustible = string.IsNullOrWhiteSpace(filtro.TipoCombustible)
            ? null
            : Catalogos.LeerCombustible(filtro.TipoCombustible, "tipo_combustible", errores);
        var vigencia = string.IsNullOrWhiteSpace(filtro.Vigencia) ? "todos" : filtro.Vigencia.Trim().ToLowerInvariant();
        if (vigencia is not ("todos" or "vigentes" or "historicos" or "futuros"))
        {
            errores.Add(new ErrorDetalle("vigencia", "Usa vigentes, historicos, futuros o todos."));
        }

        if (filtro.Desde is { } d && filtro.Hasta is { } h && d > h)
        {
            errores.Add(new ErrorDetalle("hasta", "La fecha final no puede ser anterior a la inicial."));
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var hoy = FechasElSalvador.Hoy();
        var consulta = db.FactoresEmision.AsNoTracking();
        if (tipo is not null)
        {
            consulta = consulta.Where(f => f.TipoVehiculo == tipo);
        }

        if (combustible is not null)
        {
            consulta = consulta.Where(f => f.TipoCombustible == combustible);
        }

        consulta = vigencia switch
        {
            "vigentes" => consulta.Where(f => f.VigenteDesde <= hoy && (f.VigenteHasta == null || f.VigenteHasta > hoy)),
            "historicos" => consulta.Where(f => f.VigenteHasta != null && f.VigenteHasta <= hoy),
            "futuros" => consulta.Where(f => f.VigenteDesde > hoy),
            _ => consulta,
        };

        if (filtro.Desde is { } desde)
        {
            consulta = consulta.Where(f => f.VigenteHasta == null || f.VigenteHasta > desde);
        }

        if (filtro.Hasta is { } hasta)
        {
            consulta = consulta.Where(f => f.VigenteDesde <= hasta);
        }

        var total = await consulta.CountAsync(cancelacion);
        var datos = await consulta
            .OrderBy(f => f.TipoCombustible).ThenBy(f => f.TipoVehiculo).ThenByDescending(f => f.VigenteDesde)
            .Skip((filtro.Page - 1) * filtro.Limit)
            .Take(filtro.Limit)
            .Select(f => new FactorRespuesta(
                f.FactorEmisionId, f.TipoVehiculo, f.TipoCombustible, f.FactorCo2Km, f.VigenteDesde, f.VigenteHasta, f.Fuente,
                f.VigenteDesde <= hoy && (f.VigenteHasta == null || f.VigenteHasta > hoy),
                db.Viajes.Count(v => v.FactorEmisionId == f.FactorEmisionId)))
            .ToListAsync(cancelacion);

        return (datos, new MetaPaginacion(filtro.Page, filtro.Limit, total, (int)Math.Ceiling(total / (double)filtro.Limit)));
    }

    public async Task<FactorRespuesta> ObtenerAsync(Guid id, CancellationToken cancelacion)
    {
        var factor = await db.FactoresEmision.AsNoTracking().FirstOrDefaultAsync(f => f.FactorEmisionId == id, cancelacion)
            ?? throw NoExiste();
        return await ARespuestaAsync(factor, cancelacion);
    }

    public async Task<FactorSustitucionRespuesta> SustituirAsync(
        Guid id, FactorSustituirSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var anterior = await db.FactoresEmision.FirstOrDefaultAsync(f => f.FactorEmisionId == id, cancelacion) ?? throw NoExiste();
        if (anterior.VigenteHasta is not null)
        {
            throw ApiException.Conflicto("Este factor ya tiene fecha de cierre. Solo se sustituye el factor abierto de cada combinación.");
        }

        var errores = new List<ErrorDetalle>();
        var desde = solicitud.VigenteDesde ?? FechasElSalvador.Hoy();
        var fuente = string.IsNullOrWhiteSpace(solicitud.Fuente) ? null : solicitud.Fuente.Trim();
        if (solicitud.FactorCo2Km is not > 0 or > FactorMaximo)
        {
            errores.Add(new ErrorDetalle("factor_co2_km", "Escribe un factor mayor que cero, en kg de CO₂ por km."));
        }
        else if (solicitud.FactorCo2Km == anterior.FactorCo2Km)
        {
            errores.Add(new ErrorDetalle("factor_co2_km", "El nuevo factor es igual al actual."));
        }

        if (fuente is null)
        {
            errores.Add(new ErrorDetalle("fuente", "Indica de dónde sale el nuevo factor."));
        }
        else if (fuente.Length > LargoMaximoFuente)
        {
            errores.Add(new ErrorDetalle("fuente", "Usa como máximo 200 caracteres."));
        }

        if (desde <= anterior.VigenteDesde)
        {
            errores.Add(new ErrorDetalle("vigente_desde", string.Create(CultureInfo.InvariantCulture,
                $"El nuevo factor debe empezar después del {anterior.VigenteDesde:dd/MM/yyyy}, cuando empezó el actual.")));
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var inicio = new DateTimeOffset(desde.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-6)).ToUniversalTime();
        var afectados = await db.Viajes.CountAsync(v => v.FactorEmisionId == id && v.SalidaEn >= inicio, cancelacion);
        if (afectados > 0)
        {
            throw ApiException.Conflicto(
                string.Create(CultureInfo.InvariantCulture,
                    $"Hay {afectados} viaje(s) desde esa fecha calculados con este factor. Elige una fecha posterior para no cambiar su historial."),
                [new ErrorDetalle("vigente_desde", "Ya hay viajes calculados con el factor actual en esa fecha.")]);
        }

        var nuevo = new FactorEmision
        {
            FactorEmisionId = Guid.NewGuid(),
            TipoVehiculo = anterior.TipoVehiculo,
            TipoCombustible = anterior.TipoCombustible,
            FactorCo2Km = solicitud.FactorCo2Km!.Value,
            VigenteDesde = desde,
            Fuente = fuente,
        };

        await using (var transaccion = await db.Database.BeginTransactionAsync(cancelacion))
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('ecotrack.usuario_id', {usuarioId.ToString()}, true)", cancelacion);
            anterior.VigenteHasta = desde;
            await db.SaveChangesAsync(cancelacion);
            db.FactoresEmision.Add(nuevo);
            await db.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);
        }

        return new FactorSustitucionRespuesta(
            await ARespuestaAsync(anterior, cancelacion), await ARespuestaAsync(nuevo, cancelacion));
    }

    private async Task<FactorRespuesta> ARespuestaAsync(FactorEmision f, CancellationToken cancelacion)
    {
        var hoy = FechasElSalvador.Hoy();
        var viajes = await db.Viajes.CountAsync(v => v.FactorEmisionId == f.FactorEmisionId, cancelacion);
        return new FactorRespuesta(
            f.FactorEmisionId, f.TipoVehiculo, f.TipoCombustible, f.FactorCo2Km, f.VigenteDesde, f.VigenteHasta, f.Fuente,
            f.VigenteDesde <= hoy && (f.VigenteHasta == null || f.VigenteHasta > hoy), viajes);
    }

    private static ApiException NoExiste() => ApiException.NoEncontrado("El factor de emisión no existe.");
}
