using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services;

public sealed class FactorEmisionService(AppDbContext db)
{
    private const decimal FactorMaximo = 999999.999999m;

    public async Task<FactorVigenteRespuesta> ObtenerVigenteAsync(
        string? tipoVehiculo, string? tipoCombustible, DateOnly? fecha, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var tipo = Catalogos.LeerTipo(tipoVehiculo, "tipo_vehiculo", errores);
        var combustible = Catalogos.LeerCombustible(tipoCombustible, "tipo_combustible", errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var factor = await BuscarVigente(tipo!.Value, combustible!.Value, fecha ?? FechasElSalvador.Hoy())
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

        return factor is null
            ? throw ApiException.NoEncontrado("No hay factor de emisión vigente para esa combinación.")
            : new FactorVigenteRespuesta(
                factor.FactorEmisionId, factor.TipoVehiculo, factor.TipoCombustible,
                factor.FactorCo2Km, factor.VigenteDesde, factor.VigenteHasta, factor.Fuente);
    }

    public async Task<FactorVigenteRespuesta> CrearAsync(
        FactorCrearSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var tipo = Catalogos.LeerTipo(solicitud.TipoVehiculo, "tipo_vehiculo", errores);
        var combustible = Catalogos.LeerCombustible(solicitud.TipoCombustible, "tipo_combustible", errores);
        var fuente = string.IsNullOrWhiteSpace(solicitud.Fuente) ? null : solicitud.Fuente.Trim();

        if (solicitud.FactorCo2Km is not > 0 or > FactorMaximo)
        {
            errores.Add(new ErrorDetalle("factor_co2_km", "Escribe un factor mayor que cero, en kg de CO₂ por km."));
        }

        if (fuente is null)
        {
            errores.Add(new ErrorDetalle("fuente", "Indica de dónde sale el factor."));
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var desde = solicitud.VigenteDesde ?? FechasElSalvador.Hoy();
        var solapa = await db.FactoresEmision.AnyAsync(
            f => f.TipoVehiculo == tipo && f.TipoCombustible == combustible
                && (f.VigenteHasta == null || f.VigenteHasta > desde),
            cancelacion);
        if (solapa)
        {
            throw Solape();
        }

        var factor = new FactorEmision
        {
            TipoVehiculo = tipo!.Value,
            TipoCombustible = combustible!.Value,
            FactorCo2Km = solicitud.FactorCo2Km!.Value,
            VigenteDesde = desde,
            Fuente = fuente,
        };
        db.FactoresEmision.Add(factor);
        await db.GuardarComoAsync(usuarioId, cancelacion);

        return new FactorVigenteRespuesta(
            factor.FactorEmisionId, factor.TipoVehiculo, factor.TipoCombustible,
            factor.FactorCo2Km, factor.VigenteDesde, factor.VigenteHasta, factor.Fuente);
    }

    private static ApiException Solape() => ApiException.Conflicto(
        "Ya hay un factor con esa vigencia para esta combinación.",
        [new ErrorDetalle("vigente_desde", "La fecha se solapa con otro factor de esta combinación.")]);

    public IQueryable<FactorEmision> BuscarVigente(TipoVehiculo tipo, TipoCombustible combustible, DateOnly fecha) =>
        db.FactoresEmision
            .Where(f => f.TipoVehiculo == tipo && f.TipoCombustible == combustible
                && f.VigenteDesde <= fecha && (f.VigenteHasta == null || f.VigenteHasta > fecha))
            .OrderByDescending(f => f.VigenteDesde);
}
