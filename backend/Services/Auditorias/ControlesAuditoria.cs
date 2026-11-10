using System.Globalization;
using EcoTrack.Data;
using EcoTrack.Models;
using EcoTrack.Services.Almacenamiento;
using EcoTrack.Services.Reportes;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services.Auditorias;

public sealed record FactorVigenteFila(long Orden, Guid? FactorEmisionId);

public sealed class ControlesAuditoria(
    AppDbContext db, IAlmacenamientoArchivos almacenamiento, ReporteService reportes, ParametrosAuditoria parametros)
{
    private const int HallazgosPorControl = 50;
    private const int ColumnaSalida = 7;
    private const int ColumnaLlegada = 8;
    private const decimal ToleranciaDistanciaKm = 0.01m;

    public async Task<List<HallazgoAuditoria>> EjecutarAsync(
        AuditoriaReporte auditoria, ReporteViaje reporte, ReporteVersion version, CancellationToken cancelacion)
    {
        var registro = new Registro(auditoria.AuditoriaReporteId, version.ReporteVersionId);
        var detalle = version.Huella?.Detalle.OrderBy(d => d.FilaNumero).ToList() ?? [];
        var contenido = await almacenamiento.LeerAsync(version.ClaveAlmacenamiento, cancelacion);

        ControlarIntegridad(registro, version, contenido);
        await ControlarCompletitudAsync(registro, reporte, version, detalle, cancelacion);
        await ControlarExactitudAsync(registro, detalle, cancelacion);
        ControlarValidez(registro, detalle, contenido);
        ControlarDuplicados(registro, detalle);
        await ControlarFactorVigenteAsync(registro, detalle, cancelacion);
        ControlarAtipicos(registro, detalle);

        return registro.Resultado();
    }

    private static void ControlarIntegridad(Registro registro, ReporteVersion version, byte[]? contenido)
    {
        if (contenido is null)
        {
            registro.Agregar("INTEGRIDAD_HASH", SeveridadHallazgo.Critica, null, "archivo",
                "El archivo de la versión auditada no está en el almacenamiento.", null, version.ClaveAlmacenamiento);
            return;
        }

        var hash = ReporteService.Sha256(contenido);
        if (!string.Equals(hash, version.HashSha256, StringComparison.Ordinal))
        {
            registro.Agregar("INTEGRIDAD_HASH", SeveridadHallazgo.Critica, null, "hash_sha256",
                "El hash del archivo no coincide con el registrado: el archivo cambió después de generarse.", hash, version.HashSha256);
        }
    }

    private async Task ControlarCompletitudAsync(
        Registro registro, ReporteViaje reporte, ReporteVersion version, List<HuellaReporteDetalle> detalle, CancellationToken cancelacion)
    {
        var viajes = await reportes.ConsultarViajesAsync(
            reporte.PeriodoDesde, reporte.PeriodoHasta, ReporteService.LeerFiltros(reporte.Filtros), cancelacion);

        if (viajes.Count != version.TotalFilas)
        {
            registro.Agregar("COMPLETITUD", SeveridadHallazgo.Alta, null, "total_filas",
                "La cantidad de filas del reporte no coincide con los viajes del periodo y filtros en la base de datos.",
                Numero(version.TotalFilas), Numero(viajes.Count));
        }

        var kmReporte = detalle.Sum(d => d.DistanciaKm ?? 0m);
        var kmBase = viajes.Sum(v => v.DistanciaKm);
        if (Math.Abs(kmReporte - kmBase) > ToleranciaDistanciaKm)
        {
            registro.Agregar("COMPLETITUD", SeveridadHallazgo.Alta, null, "distancia",
                "La distancia total del reporte no coincide con la de los viajes en la base de datos.",
                Numero(kmReporte), Numero(kmBase));
        }
    }

    private async Task ControlarExactitudAsync(Registro registro, List<HuellaReporteDetalle> detalle, CancellationToken cancelacion)
    {
        var validas = detalle.Where(d => d.Estado == EstadoDetalleHuella.Ok).ToList();
        var ids = validas.Select(d => d.FactorEmisionId!.Value).Distinct().ToList();
        var factores = await db.FactoresEmision.AsNoTracking()
            .Where(f => ids.Contains(f.FactorEmisionId))
            .ToDictionaryAsync(f => f.FactorEmisionId, f => f.FactorCo2Km, cancelacion);

        foreach (var fila in validas)
        {
            var recalculado = Math.Round(fila.DistanciaKm!.Value * factores[fila.FactorEmisionId!.Value], 4, MidpointRounding.AwayFromZero);
            if (Math.Abs(recalculado - fila.Co2Kg!.Value) > parametros.ToleranciaCo2Kg)
            {
                registro.Agregar("EXACTITUD_CO2", SeveridadHallazgo.Alta, fila.FilaNumero, "co2_kg",
                    "El CO₂ recalculado de forma independiente no coincide con el calculado para la fila.",
                    Numero(fila.Co2Kg.Value), Numero(recalculado));
            }

            if (fila.Co2KgOperativo is { } operativo && Math.Abs(operativo - fila.Co2Kg.Value) > parametros.ToleranciaCo2Kg)
            {
                registro.Agregar("EXACTITUD_CO2", SeveridadHallazgo.Alta, fila.FilaNumero, "co2_kg_operativo",
                    "El CO₂ que tenía el sistema al generar el reporte no coincide con el calculado.",
                    Numero(operativo), Numero(fila.Co2Kg.Value));
            }
        }
    }

    private static void ControlarValidez(Registro registro, List<HuellaReporteDetalle> detalle, byte[]? contenido)
    {
        foreach (var fila in detalle.Where(d => d.Estado == EstadoDetalleHuella.Error))
        {
            registro.Agregar("VALIDEZ", SeveridadHallazgo.Media, fila.FilaNumero, null,
                fila.MensajeError ?? "La fila no se pudo calcular.", null, null);
        }

        if (contenido is null)
        {
            return;
        }

        foreach (var fila in ReporteCsv.Leer(contenido).Filas.Where(f => f.Campos.Count == ReporteCsv.Columnas.Count))
        {
            if (ReporteCsv.LeerFecha(fila.Campos[ColumnaSalida], out var salida)
                && ReporteCsv.LeerFecha(fila.Campos[ColumnaLlegada], out var llegada)
                && llegada < salida)
            {
                registro.Agregar("VALIDEZ", SeveridadHallazgo.Media, fila.Numero, "fecha_llegada",
                    "La fecha de llegada es anterior a la de salida.", fila.Campos[ColumnaLlegada], "Igual o posterior a " + fila.Campos[ColumnaSalida]);
            }
        }
    }

    private static void ControlarDuplicados(Registro registro, List<HuellaReporteDetalle> detalle)
    {
        var grupos = detalle
            .Where(d => d.ViajeReferencia is not null)
            .GroupBy(d => d.ViajeReferencia!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var grupo in grupos)
        {
            var primera = grupo.First().FilaNumero;
            foreach (var repetida in grupo.Skip(1))
            {
                registro.Agregar("DUPLICADO", SeveridadHallazgo.Alta, repetida.FilaNumero, "viaje_id",
                    string.Create(CultureInfo.InvariantCulture, $"El viaje aparece repetido; ya estaba en la fila {primera}."),
                    grupo.Key, null);
            }
        }
    }

    private async Task ControlarFactorVigenteAsync(Registro registro, List<HuellaReporteDetalle> detalle, CancellationToken cancelacion)
    {
        var validas = detalle.Where(d => d.Estado == EstadoDetalleHuella.Ok).ToList();
        if (validas.Count == 0)
        {
            return;
        }

        var combustibles = validas.Select(d => d.TipoCombustible!).ToArray();
        var tipos = validas.Select(d => d.TipoVehiculo!).ToArray();
        var salidas = validas.Select(d => d.SalidaEn!.Value).ToArray();

        var vigentes = await db.Database.SqlQuery<FactorVigenteFila>($"""
            SELECT t.orden AS "Orden",
                   factor_emision_vigente(t.combustible::tipo_combustible_enum, t.tipo::tipo_vehiculo_enum,
                       (t.salida AT TIME ZONE 'America/El_Salvador')::date) AS "FactorEmisionId"
              FROM unnest({combustibles}::text[], {tipos}::text[], {salidas}::timestamptz[])
                   WITH ORDINALITY AS t(combustible, tipo, salida, orden)
             ORDER BY t.orden
            """).ToListAsync(cancelacion);

        foreach (var vigente in vigentes)
        {
            var fila = validas[(int)vigente.Orden - 1];
            if (vigente.FactorEmisionId != fila.FactorEmisionId)
            {
                registro.Agregar("FACTOR_VIGENTE", SeveridadHallazgo.Alta, fila.FilaNumero, "factor_emision",
                    "El factor aplicado no es el vigente para la fecha de salida, el combustible y el tipo de vehículo.",
                    fila.FactorEmisionId?.ToString(), vigente.FactorEmisionId?.ToString());
            }
        }
    }

    private void ControlarAtipicos(Registro registro, List<HuellaReporteDetalle> detalle)
    {
        foreach (var fila in detalle.Where(d => d.DistanciaKm > parametros.UmbralDistanciaKm))
        {
            registro.Agregar("ATIPICO", SeveridadHallazgo.Observacion, fila.FilaNumero, "distancia",
                "La distancia supera el umbral configurado para viajes atípicos.",
                Numero(fila.DistanciaKm!.Value), "Hasta " + Numero(parametros.UmbralDistanciaKm));
        }
    }

    private static string Numero(decimal valor) => valor.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Numero(int valor) => valor.ToString(CultureInfo.InvariantCulture);

    private sealed class Registro(Guid auditoriaId, Guid versionId)
    {
        private readonly List<HallazgoAuditoria> hallazgos = [];
        private readonly Dictionary<string, int> omitidos = [];

        public void Agregar(
            string control, SeveridadHallazgo severidad, int? fila, string? campo, string descripcion, string? encontrado, string? esperado)
        {
            if (hallazgos.Count(h => h.Control == control) >= HallazgosPorControl)
            {
                omitidos[control] = omitidos.GetValueOrDefault(control) + 1;
                return;
            }

            hallazgos.Add(Crear(control, severidad, fila, campo, descripcion, encontrado, esperado));
        }

        public List<HallazgoAuditoria> Resultado()
        {
            foreach (var (control, cantidad) in omitidos)
            {
                hallazgos.Add(Crear(control, SeveridadHallazgo.Observacion, null, null,
                    string.Create(CultureInfo.InvariantCulture,
                        $"Hay {cantidad} hallazgos más del control {control} que no se listan; revisa las filas del reporte."),
                    null, null));
            }

            return hallazgos;
        }

        private HallazgoAuditoria Crear(
            string control, SeveridadHallazgo severidad, int? fila, string? campo, string descripcion, string? encontrado, string? esperado) => new()
            {
                HallazgoAuditoriaId = Guid.NewGuid(),
                AuditoriaReporteId = auditoriaId,
                VersionOrigenId = versionId,
                Origen = OrigenHallazgo.Automatico,
                Control = control,
                Severidad = severidad,
                FilaNumero = fila,
                Campo = campo,
                Descripcion = descripcion,
                ValorEncontrado = encontrado,
                ValorEsperado = esperado,
                Estado = EstadoHallazgo.Abierto,
                DetectadoEn = DateTimeOffset.UtcNow,
            };
    }
}
