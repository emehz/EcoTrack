using System.Globalization;
using System.Text.Json;
using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EcoTrack.Services;

public sealed class PanelService(AppDbContext db)
{
    public const int TopMaximo = 20;
    private static readonly TimeSpan HoraElSalvador = TimeSpan.FromHours(-6);

    private const string Filtro = """
        v.estado = 'completado'
        AND v.salida_en >= @inicio AND v.salida_en < @fin
        AND (@tipo = '' OR f.tipo_vehiculo::text = @tipo)
        AND (@combustible = '' OR f.tipo_combustible::text = @combustible)
        """;

    private const string AgrupadoCombustible = $"""
        SELECT f.tipo_combustible::text AS "Nombre", count(*)::int AS "Viajes", sum(v.co2_kg) AS "Co2"
          FROM viaje v
          JOIN factor_emision f ON f.factor_emision_id = v.factor_emision_id
         WHERE {Filtro}
         GROUP BY 1
         ORDER BY 3 DESC
        """;

    private const string AgrupadoTipo = $"""
        SELECT f.tipo_vehiculo::text AS "Nombre", count(*)::int AS "Viajes", sum(v.co2_kg) AS "Co2"
          FROM viaje v
          JOIN factor_emision f ON f.factor_emision_id = v.factor_emision_id
         WHERE {Filtro}
         GROUP BY 1
         ORDER BY 3 DESC
        """;

    private sealed record Cifras(int Viajes, decimal Km, decimal Co2);

    private sealed record Grupo(string Nombre, int Viajes, decimal Co2);

    private sealed record Emisor(Guid VehiculoId, string Placa, string TipoVehiculo, string TipoCombustible, int Viajes, decimal Km, decimal Co2);

    private sealed record Mes(string Clave, decimal Co2);

    private sealed record Flota(int Activos, int ElectricosHibridos);

    public async Task<PanelRespuesta> ObtenerAsync(PanelFiltro filtro, CancellationToken cancelacion)
    {
        var (desde, hasta, tipo, combustible) = Validar(filtro);
        var dias = hasta.DayNumber - desde.DayNumber + 1;
        var anteriorHasta = desde.AddDays(-1);
        var anteriorDesde = anteriorHasta.AddDays(-(dias - 1));
        var inicioAnio = new DateOnly(hasta.Year, 1, 1);

        var actual = await CifrasAsync(desde, hasta, tipo, combustible, cancelacion);
        var anterior = await CifrasAsync(anteriorDesde, anteriorHasta, tipo, combustible, cancelacion);
        var anio = await CifrasAsync(inicioAnio, hasta, tipo, combustible, cancelacion);

        var porCombustible = await AgruparAsync(AgrupadoCombustible, desde, hasta, tipo, combustible, cancelacion);
        var porTipo = await AgruparAsync(AgrupadoTipo, desde, hasta, tipo, combustible, cancelacion);

        var top = await db.Database.SqlQueryRaw<Emisor>($"""
            SELECT ve.vehiculo_id AS "VehiculoId", ve.placa AS "Placa",
                   ve.tipo_vehiculo::text AS "TipoVehiculo", ve.tipo_combustible::text AS "TipoCombustible",
                   count(*)::int AS "Viajes", sum(v.distancia_km) AS "Km", sum(v.co2_kg) AS "Co2"
              FROM viaje v
              JOIN factor_emision f ON f.factor_emision_id = v.factor_emision_id
              JOIN vehiculo ve ON ve.vehiculo_id = v.vehiculo_id
             WHERE {Filtro}
             GROUP BY ve.vehiculo_id, ve.placa, ve.tipo_vehiculo, ve.tipo_combustible
             ORDER BY sum(v.co2_kg) DESC, ve.placa
             LIMIT @top
            """, [.. Parametros(desde, hasta, tipo, combustible), new NpgsqlParameter("top", filtro.Top)]).ToListAsync(cancelacion);

        var meses = await db.Database.SqlQueryRaw<Mes>($"""
            SELECT to_char(v.salida_en AT TIME ZONE 'America/El_Salvador', 'YYYY-MM') AS "Clave", sum(v.co2_kg) AS "Co2"
              FROM viaje v
              JOIN factor_emision f ON f.factor_emision_id = v.factor_emision_id
             WHERE {Filtro}
             GROUP BY 1
            """, Parametros(desde, hasta, tipo, combustible)).ToListAsync(cancelacion);

        var flota = await db.Database.SqlQueryRaw<Flota>("""
            SELECT count(*)::int AS "Activos",
                   (count(*) FILTER (WHERE tipo_combustible IN ('electrico', 'hibrido')))::int AS "ElectricosHibridos"
              FROM vehiculo
             WHERE estado = 'activo'
               AND (@tipo = '' OR tipo_vehiculo::text = @tipo)
               AND (@combustible = '' OR tipo_combustible::text = @combustible)
            """, new NpgsqlParameter("tipo", tipo), new NpgsqlParameter("combustible", combustible)).FirstAsync(cancelacion);

        var intensidad = Intensidad(actual);
        var intensidadAnterior = Intensidad(anterior);
        decimal? variacion = intensidad is { } i && intensidadAnterior is > 0
            ? Math.Round((i - intensidadAnterior.Value) / intensidadAnterior.Value * 100, 1)
            : null;

        return new PanelRespuesta(
            new PeriodoPanel(desde, hasta),
            ACifras(actual),
            new PeriodoPanel(anteriorDesde, anteriorHasta),
            ACifras(anterior),
            variacion,
            ACifras(anio),
            new FlotaPanel(flota.Activos, flota.ElectricosHibridos),
            porCombustible,
            porTipo,
            top.Select(t => new VehiculoEmisor(t.VehiculoId, t.Placa, t.TipoVehiculo, t.TipoCombustible, t.Viajes, t.Km, t.Co2)).ToList(),
            Tendencia(desde, hasta, meses));
    }

    private static (DateOnly Desde, DateOnly Hasta, string Tipo, string Combustible) Validar(PanelFiltro filtro)
    {
        var errores = new List<ErrorDetalle>();
        var hasta = filtro.Hasta ?? FechasElSalvador.Hoy();
        var desde = filtro.Desde ?? new DateOnly(hasta.Year, 1, 1);
        if (desde > hasta)
        {
            errores.Add(new ErrorDetalle("hasta", "La fecha final no puede ser anterior a la inicial."));
        }

        if (filtro.Top is < 1 or > TopMaximo)
        {
            errores.Add(new ErrorDetalle("top", string.Create(CultureInfo.InvariantCulture, $"El número de vehículos debe estar entre 1 y {TopMaximo}.")));
        }

        var tipo = string.IsNullOrWhiteSpace(filtro.TipoVehiculo) ? null : Catalogos.LeerTipo(filtro.TipoVehiculo, "tipo_vehiculo", errores);
        var combustible = string.IsNullOrWhiteSpace(filtro.TipoCombustible)
            ? null
            : Catalogos.LeerCombustible(filtro.TipoCombustible, "tipo_combustible", errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        return (desde, hasta, Texto(tipo?.ToString()), Texto(combustible?.ToString()));
    }

    private async Task<Cifras> CifrasAsync(
        DateOnly desde, DateOnly hasta, string tipo, string combustible, CancellationToken cancelacion) =>
        await db.Database.SqlQueryRaw<Cifras>($"""
            SELECT count(*)::int AS "Viajes", coalesce(sum(v.distancia_km), 0) AS "Km", coalesce(sum(v.co2_kg), 0) AS "Co2"
              FROM viaje v
              JOIN factor_emision f ON f.factor_emision_id = v.factor_emision_id
             WHERE {Filtro}
            """, Parametros(desde, hasta, tipo, combustible)).FirstAsync(cancelacion);

    private async Task<IReadOnlyList<EmisionPorGrupo>> AgruparAsync(
        string consulta, DateOnly desde, DateOnly hasta, string tipo, string combustible, CancellationToken cancelacion)
    {
        var grupos = await db.Database.SqlQueryRaw<Grupo>(consulta, Parametros(desde, hasta, tipo, combustible)).ToListAsync(cancelacion);
        return grupos.Select(g => new EmisionPorGrupo(g.Nombre, g.Viajes, g.Co2)).ToList();
    }

    private static NpgsqlParameter[] Parametros(DateOnly desde, DateOnly hasta, string tipo, string combustible) =>
    [
        new("inicio", InicioDelDia(desde)),
        new("fin", InicioDelDia(hasta.AddDays(1))),
        new("tipo", tipo),
        new("combustible", combustible),
    ];

    private static List<MesPanel> Tendencia(DateOnly desde, DateOnly hasta, List<Mes> meses)
    {
        var resultado = new List<MesPanel>();
        for (var mes = new DateOnly(desde.Year, desde.Month, 1); mes <= hasta; mes = mes.AddMonths(1))
        {
            var clave = mes.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            resultado.Add(new MesPanel(clave, meses.Find(m => m.Clave == clave)?.Co2 ?? 0));
        }

        return resultado;
    }

    private static decimal? Intensidad(Cifras c) => c.Km > 0 ? Math.Round(c.Co2 / c.Km, 4) : null;

    private static CifrasPanel ACifras(Cifras c) =>
        new(c.Viajes, c.Km, c.Co2, Math.Round(c.Co2 / 1000, 3), Intensidad(c));

    private static DateTimeOffset InicioDelDia(DateOnly fecha) =>
        new DateTimeOffset(fecha.ToDateTime(TimeOnly.MinValue), HoraElSalvador).ToUniversalTime();

    private static string Texto(string? valor) => valor is null ? string.Empty : JsonNamingPolicy.SnakeCaseLower.ConvertName(valor);
}
