using System.Globalization;
using System.Text.Json;
using EcoTrack.Data;
using EcoTrack.Models;
using EcoTrack.Services.Almacenamiento;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services.Reportes;

public sealed record ResultadoFilaHuella(long Orden, Guid? FactorEmisionId, decimal? Co2Kg, string? Error);

public sealed class CalculadorHuella(AppDbContext db, IAlmacenamientoArchivos almacenamiento)
{
    private const int ColumnaViaje = 0;
    private const int ColumnaPlaca = 1;
    private const int ColumnaTipo = 2;
    private const int ColumnaCombustible = 3;
    private const int ColumnaSalida = 7;
    private const int ColumnaDistancia = 9;
    private const int ColumnaCo2Operativo = 11;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task CalcularAsync(Guid versionId, CancellationToken cancelacion)
    {
        var version = await db.ReporteVersiones
            .Include(v => v.Reporte)
            .FirstAsync(v => v.ReporteVersionId == versionId, cancelacion);

        var contenido = await almacenamiento.LeerAsync(version.ClaveAlmacenamiento, cancelacion);
        if (contenido is null)
        {
            await FallarAsync(version, "El archivo de la versión no está en el almacenamiento.", cancelacion);
            return;
        }

        if (!string.Equals(ReporteService.Sha256(contenido), version.HashSha256, StringComparison.Ordinal))
        {
            await FallarAsync(version,
                "El hash SHA-256 del archivo no coincide con el registrado. El cálculo se detuvo porque el archivo pudo ser alterado.",
                cancelacion);
            return;
        }

        var (encabezado, filas) = ReporteCsv.Leer(contenido);
        if (!encabezado.SequenceEqual(ReporteCsv.Columnas))
        {
            await FallarAsync(version, "El encabezado del CSV no tiene las columnas esperadas.", cancelacion);
            return;
        }

        var detalle = await CalcularFilasAsync(filas, cancelacion);
        var validas = detalle.Where(d => d.Estado == EstadoDetalleHuella.Ok).ToList();
        var huella = new HuellaReporte
        {
            HuellaReporteId = Guid.NewGuid(),
            ReporteVersionId = version.ReporteVersionId,
            FilasProcesadas = detalle.Count,
            FilasValidas = validas.Count,
            FilasConError = detalle.Count - validas.Count,
            DistanciaTotalKm = validas.Sum(d => d.DistanciaKm!.Value),
            Co2TotalKg = validas.Sum(d => d.Co2Kg!.Value),
            ClaveResultado = ClavesAlmacenamiento.ResultadoHuella(version.ReporteViajeId, version.NumeroVersion),
            CalculadoEn = DateTimeOffset.UtcNow,
        };
        huella.Detalle.AddRange(detalle);

        await almacenamiento.GuardarAsync(huella.ClaveResultado, ResultadoJson(version, huella), permitirReemplazo: true, cancelacion);

        db.HuellasReporte.Add(huella);
        version.EstadoCalculo = EstadoCalculo.Completado;
        version.MensajeError = null;
        version.CalculoTerminadoEn = DateTimeOffset.UtcNow;
        if (version.Reporte!.Estado is EstadoReporte.Generado or EstadoReporte.Subido)
        {
            version.Reporte.Estado = EstadoReporte.Calculado;
        }

        await db.GuardarComoAsync(version.CreadoPorUsuarioId, cancelacion);
    }

    public async Task MarcarFallidoAsync(Guid versionId, string mensaje, CancellationToken cancelacion)
    {
        db.ChangeTracker.Clear();
        var version = await db.ReporteVersiones.FirstAsync(v => v.ReporteVersionId == versionId, cancelacion);
        await FallarAsync(version, mensaje, cancelacion);
    }

    private async Task<List<HuellaReporteDetalle>> CalcularFilasAsync(IReadOnlyList<FilaCsv> filas, CancellationToken cancelacion)
    {
        var detalle = new List<HuellaReporteDetalle>(filas.Count);
        var porCalcular = new List<(HuellaReporteDetalle Fila, string Combustible, string Tipo, DateTimeOffset Salida, decimal Distancia)>();

        foreach (var fila in filas)
        {
            var actual = new HuellaReporteDetalle { FilaNumero = fila.Numero, Estado = EstadoDetalleHuella.Error };
            detalle.Add(actual);

            if (fila.Campos.Count != ReporteCsv.Columnas.Count)
            {
                actual.MensajeError = string.Create(CultureInfo.InvariantCulture,
                    $"La fila tiene {fila.Campos.Count} columnas y se esperaban {ReporteCsv.Columnas.Count}.");
                continue;
            }

            var campos = fila.Campos;
            actual.ViajeReferencia = Texto(campos[ColumnaViaje]);
            actual.VehiculoPlaca = Texto(campos[ColumnaPlaca]);
            actual.TipoVehiculo = Texto(campos[ColumnaTipo]);
            actual.TipoCombustible = Texto(campos[ColumnaCombustible]);
            if (ReporteCsv.LeerDecimal(campos[ColumnaCo2Operativo], out var operativo))
            {
                actual.Co2KgOperativo = operativo;
            }

            var hayFecha = ReporteCsv.LeerFecha(campos[ColumnaSalida], out var salida);
            var hayDistancia = ReporteCsv.LeerDecimal(campos[ColumnaDistancia], out var distancia);
            if (hayFecha)
            {
                actual.SalidaEn = salida.ToUniversalTime();
            }

            if (hayDistancia)
            {
                actual.DistanciaKm = distancia;
            }

            if (!hayFecha)
            {
                actual.MensajeError = "La fecha de salida no tiene un formato válido.";
                continue;
            }

            if (!hayDistancia)
            {
                actual.MensajeError = "La distancia no es un número válido.";
                continue;
            }

            porCalcular.Add((actual, campos[ColumnaCombustible], campos[ColumnaTipo], salida.ToUniversalTime(), distancia));
        }

        if (porCalcular.Count == 0)
        {
            return detalle;
        }

        var combustibles = porCalcular.Select(p => p.Combustible).ToArray();
        var tipos = porCalcular.Select(p => p.Tipo).ToArray();
        var salidas = porCalcular.Select(p => p.Salida).ToArray();
        var distancias = porCalcular.Select(p => p.Distancia).ToArray();

        var resultados = await db.Database.SqlQuery<ResultadoFilaHuella>($"""
            SELECT t.orden AS "Orden",
                   r.o_factor_emision_id AS "FactorEmisionId",
                   r.o_co2_kg AS "Co2Kg",
                   r.o_error AS "Error"
              FROM unnest({combustibles}::text[], {tipos}::text[], {salidas}::timestamptz[], {distancias}::numeric[])
                   WITH ORDINALITY AS t(combustible, tipo, salida, distancia, orden)
             CROSS JOIN LATERAL huella_calcular_fila(t.combustible, t.tipo, t.salida, t.distancia) r
             ORDER BY t.orden
            """).ToListAsync(cancelacion);

        foreach (var resultado in resultados)
        {
            var fila = porCalcular[(int)resultado.Orden - 1].Fila;
            if (resultado.Error is null)
            {
                fila.Estado = EstadoDetalleHuella.Ok;
                fila.FactorEmisionId = resultado.FactorEmisionId;
                fila.Co2Kg = resultado.Co2Kg;
            }
            else
            {
                fila.MensajeError = resultado.Error;
            }
        }

        return detalle;
    }

    private async Task FallarAsync(ReporteVersion version, string mensaje, CancellationToken cancelacion)
    {
        version.EstadoCalculo = EstadoCalculo.Fallido;
        version.MensajeError = mensaje;
        version.CalculoTerminadoEn = DateTimeOffset.UtcNow;
        await db.GuardarComoAsync(version.CreadoPorUsuarioId, cancelacion);
    }

    private static byte[] ResultadoJson(ReporteVersion version, HuellaReporte huella) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            ReporteViajeId = version.ReporteViajeId,
            NumeroVersion = version.NumeroVersion,
            HashSha256 = version.HashSha256,
            huella.CalculadoEn,
            huella.FilasProcesadas,
            huella.FilasValidas,
            huella.FilasConError,
            huella.DistanciaTotalKm,
            huella.Co2TotalKg,
            Co2TotalToneladas = Math.Round(huella.Co2TotalKg / 1000m, 4),
            Filas = huella.Detalle.Select(d => new
            {
                Fila = d.FilaNumero,
                Viaje = d.ViajeReferencia,
                Placa = d.VehiculoPlaca,
                d.TipoVehiculo,
                d.TipoCombustible,
                d.SalidaEn,
                d.DistanciaKm,
                d.FactorEmisionId,
                d.Co2Kg,
                d.Co2KgOperativo,
                Estado = d.Estado == EstadoDetalleHuella.Ok ? "ok" : "error",
                d.MensajeError,
            }),
        }, Json);

    private static string? Texto(string valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
