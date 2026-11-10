using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using EcoTrack.Services.Almacenamiento;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services.Reportes;

public sealed class ReporteService(AppDbContext db, IAlmacenamientoArchivos almacenamiento, SenalCalculoHuella senal)
{
    public const int LimiteMaximo = 100;
    public const int FilasMaximas = 10_000;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private static readonly Dictionary<string, EstadoReporte> Estados = new()
    {
        ["generado"] = EstadoReporte.Generado,
        ["subido"] = EstadoReporte.Subido,
        ["calculado"] = EstadoReporte.Calculado,
        ["en_auditoria"] = EstadoReporte.EnAuditoria,
        ["auditado"] = EstadoReporte.Auditado,
        ["fallido"] = EstadoReporte.Fallido,
    };

    public static string Sha256(byte[] contenido) => Convert.ToHexStringLower(SHA256.HashData(contenido));

    public static FiltrosReporte LeerFiltros(string json) =>
        JsonSerializer.Deserialize<FiltrosReporte>(json, Json) ?? new FiltrosReporte(null, null, null, null, true);

    public async Task<ReporteDetalleRespuesta> GenerarAsync(
        ReporteGenerarSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        if (solicitud.Desde is null)
        {
            errores.Add(new ErrorDetalle("desde", "Indica la fecha inicial del periodo."));
        }

        if (solicitud.Hasta is null)
        {
            errores.Add(new ErrorDetalle("hasta", "Indica la fecha final del periodo."));
        }

        if (solicitud is { Desde: { } inicio, Hasta: { } fin } && fin < inicio)
        {
            errores.Add(new ErrorDetalle("hasta", "La fecha final no puede ser anterior a la inicial."));
        }

        var tipo = string.IsNullOrWhiteSpace(solicitud.TipoVehiculo)
            ? null
            : Catalogos.LeerTipo(solicitud.TipoVehiculo, "tipo_vehiculo", errores)?.ToString().ToLowerInvariant();
        var combustible = string.IsNullOrWhiteSpace(solicitud.TipoCombustible)
            ? null
            : Catalogos.LeerCombustible(solicitud.TipoCombustible, "tipo_combustible", errores)?.ToString().ToLowerInvariant();

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var desde = solicitud.Desde!.Value;
        var hasta = solicitud.Hasta!.Value;
        var filtros = new FiltrosReporte(solicitud.VehiculoId, solicitud.ChoferId, tipo, combustible, solicitud.SoloCompletados ?? true);
        var filas = await ConsultarViajesAsync(desde, hasta, filtros, cancelacion);

        if (filas.Count == 0)
        {
            throw ApiException.Validacion([new ErrorDetalle("desde", "No hay viajes en ese periodo con los filtros elegidos.")]);
        }

        if (filas.Count > FilasMaximas)
        {
            throw ApiException.Validacion([new ErrorDetalle("desde",
                string.Create(CultureInfo.InvariantCulture, $"El reporte tendría {filas.Count} filas; el máximo es {FilasMaximas}. Reduce el periodo."))]);
        }

        var ahora = DateTimeOffset.UtcNow;
        var reporte = new ReporteViaje
        {
            ReporteViajeId = Guid.NewGuid(),
            NombreArchivo = string.Create(CultureInfo.InvariantCulture, $"reporte-viajes-{desde:yyyy-MM-dd}-a-{hasta:yyyy-MM-dd}.csv"),
            PeriodoDesde = desde,
            PeriodoHasta = hasta,
            Filtros = JsonSerializer.Serialize(filtros, Json),
            TotalFilas = filas.Count,
            Estado = EstadoReporte.Generado,
            GeneradoPorUsuarioId = usuarioId,
            GeneradoEn = ahora,
            ActualizadoEn = ahora,
        };
        db.ReportesViaje.Add(reporte);
        await db.GuardarComoAsync(usuarioId, cancelacion);

        var contenido = ReporteCsv.Escribir(filas);
        var clave = ClavesAlmacenamiento.Reporte(reporte.ReporteViajeId, 1);
        try
        {
            await almacenamiento.GuardarAsync(clave, contenido, permitirReemplazo: false, cancelacion);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            reporte.Estado = EstadoReporte.Fallido;
            reporte.MensajeError = "No se pudo guardar el archivo en el almacenamiento. Genera el reporte de nuevo.";
            await db.GuardarComoAsync(usuarioId, cancelacion);
            return await ObtenerAsync(reporte.ReporteViajeId, cancelacion);
        }

        db.ReporteVersiones.Add(new ReporteVersion
        {
            ReporteVersionId = Guid.NewGuid(),
            ReporteViajeId = reporte.ReporteViajeId,
            NumeroVersion = 1,
            Origen = OrigenVersion.Original,
            ClaveAlmacenamiento = clave,
            HashSha256 = Sha256(contenido),
            TamanoBytes = contenido.LongLength,
            TotalFilas = filas.Count,
            EstadoCalculo = EstadoCalculo.Pendiente,
            CreadoPorUsuarioId = usuarioId,
            CreadoEn = DateTimeOffset.UtcNow,
        });
        reporte.Estado = EstadoReporte.Subido;
        await db.GuardarComoAsync(usuarioId, cancelacion);
        senal.Notificar();

        return await ObtenerAsync(reporte.ReporteViajeId, cancelacion);
    }

    public Task<List<FilaViajeReporte>> ConsultarViajesAsync(
        DateOnly desde, DateOnly hasta, FiltrosReporte filtros, CancellationToken cancelacion) =>
        db.Database.SqlQuery<FilaViajeReporte>($"""
            SELECT v.viaje_id AS "ViajeId",
                   ve.placa AS "Placa",
                   fe.tipo_vehiculo::text AS "TipoVehiculo",
                   fe.tipo_combustible::text AS "TipoCombustible",
                   ch.numero_licencia AS "ChoferLicencia",
                   v.origen AS "Origen",
                   v.destino AS "Destino",
                   v.salida_en AS "SalidaEn",
                   v.llegada_en AS "LlegadaEn",
                   v.distancia_km AS "DistanciaKm",
                   v.estado::text AS "Estado",
                   v.co2_kg AS "Co2Kg"
              FROM viaje v
              JOIN vehiculo ve ON ve.vehiculo_id = v.vehiculo_id
              JOIN factor_emision fe ON fe.factor_emision_id = v.factor_emision_id
              LEFT JOIN chofer ch ON ch.chofer_id = v.chofer_id
             WHERE (v.salida_en AT TIME ZONE 'America/El_Salvador')::date BETWEEN {desde} AND {hasta}
               AND v.estado <> 'cancelado'
               AND (NOT {filtros.SoloCompletados} OR v.estado = 'completado')
               AND ({filtros.VehiculoId}::uuid IS NULL OR v.vehiculo_id = {filtros.VehiculoId})
               AND ({filtros.ChoferId}::uuid IS NULL OR v.chofer_id = {filtros.ChoferId})
               AND ({filtros.TipoVehiculo}::text IS NULL OR fe.tipo_vehiculo::text = {filtros.TipoVehiculo})
               AND ({filtros.TipoCombustible}::text IS NULL OR fe.tipo_combustible::text = {filtros.TipoCombustible})
             ORDER BY v.salida_en, v.viaje_id
            """).ToListAsync(cancelacion);

    public async Task<(IReadOnlyList<ReporteResumenRespuesta> Datos, MetaPaginacion Meta)> ListarAsync(
        ReporteFiltro filtro, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        EstadoReporte? estado = null;
        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            if (Estados.TryGetValue(filtro.Estado.Trim().ToLowerInvariant(), out var encontrado))
            {
                estado = encontrado;
            }
            else
            {
                errores.Add(new ErrorDetalle("estado", "Elige un estado de reporte válido."));
            }
        }

        ValidarPaginacion(filtro.Page, filtro.Limit, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var consulta = db.ReportesViaje.AsNoTracking();
        if (estado is not null)
        {
            consulta = consulta.Where(r => r.Estado == estado);
        }

        if (filtro.Desde is { } desde)
        {
            consulta = consulta.Where(r => r.PeriodoHasta >= desde);
        }

        if (filtro.Hasta is { } hasta)
        {
            consulta = consulta.Where(r => r.PeriodoDesde <= hasta);
        }

        var total = await consulta.CountAsync(cancelacion);
        var datos = await consulta
            .OrderByDescending(r => r.GeneradoEn)
            .Skip((filtro.Page - 1) * filtro.Limit)
            .Take(filtro.Limit)
            .Select(r => new ReporteResumenRespuesta(
                r.ReporteViajeId,
                r.NombreArchivo,
                r.PeriodoDesde,
                r.PeriodoHasta,
                r.Estado,
                r.TotalFilas,
                r.GeneradoPor!.Nombres + " " + r.GeneradoPor.Apellidos,
                r.GeneradoEn,
                r.Versiones.Count,
                r.Versiones.Where(v => v.EsOficial).Select(v => (int?)v.NumeroVersion).FirstOrDefault(),
                r.Versiones.OrderByDescending(v => v.EsOficial).ThenByDescending(v => v.NumeroVersion)
                    .Select(v => (EstadoCalculo?)v.EstadoCalculo).FirstOrDefault(),
                r.Versiones.OrderByDescending(v => v.EsOficial).ThenByDescending(v => v.NumeroVersion)
                    .Select(v => (decimal?)v.Huella!.Co2TotalKg).FirstOrDefault(),
                r.Versiones.OrderByDescending(v => v.EsOficial).ThenByDescending(v => v.NumeroVersion)
                    .Select(v => (decimal?)v.Huella!.DistanciaTotalKm).FirstOrDefault()))
            .ToListAsync(cancelacion);

        var paginas = (int)Math.Ceiling(total / (double)filtro.Limit);
        return (datos, new MetaPaginacion(filtro.Page, filtro.Limit, total, paginas));
    }

    public async Task<ReporteDetalleRespuesta> ObtenerAsync(Guid id, CancellationToken cancelacion)
    {
        var reporte = await db.ReportesViaje.AsNoTracking()
            .Include(r => r.GeneradoPor)
            .Include(r => r.Versiones).ThenInclude(v => v.Huella)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.ReporteViajeId == id, cancelacion)
            ?? throw ApiException.NoEncontrado("El reporte no existe.");

        return new ReporteDetalleRespuesta(
            reporte.ReporteViajeId,
            reporte.NombreArchivo,
            reporte.PeriodoDesde,
            reporte.PeriodoHasta,
            LeerFiltros(reporte.Filtros),
            reporte.Estado,
            reporte.MensajeError,
            reporte.TotalFilas,
            $"{reporte.GeneradoPor?.Nombres} {reporte.GeneradoPor?.Apellidos}".Trim(),
            reporte.GeneradoEn,
            reporte.Versiones.OrderBy(v => v.NumeroVersion).Select(AVersion).ToList());
    }

    public async Task<(IReadOnlyList<FilaHuellaRespuesta> Datos, MetaPaginacion Meta)> ListarFilasAsync(
        Guid id, int numero, FilasFiltro filtro, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        EstadoDetalleHuella? estado = filtro.Estado?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "ok" => EstadoDetalleHuella.Ok,
            "error" => EstadoDetalleHuella.Error,
            _ => Invalido(errores),
        };
        ValidarPaginacion(filtro.Page, filtro.Limit, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var version = await BuscarVersionAsync(id, numero, cancelacion);
        var huellaId = await db.HuellasReporte.AsNoTracking()
            .Where(h => h.ReporteVersionId == version.ReporteVersionId)
            .Select(h => (Guid?)h.HuellaReporteId)
            .FirstOrDefaultAsync(cancelacion);

        if (huellaId is null)
        {
            return ([], new MetaPaginacion(filtro.Page, filtro.Limit, 0, 0));
        }

        var consulta = db.HuellasReporteDetalle.AsNoTracking().Where(d => d.HuellaReporteId == huellaId);
        if (estado is not null)
        {
            consulta = consulta.Where(d => d.Estado == estado);
        }

        var total = await consulta.CountAsync(cancelacion);
        var datos = await consulta
            .OrderBy(d => d.FilaNumero)
            .Skip((filtro.Page - 1) * filtro.Limit)
            .Take(filtro.Limit)
            .Select(d => new FilaHuellaRespuesta(
                d.FilaNumero,
                d.ViajeReferencia,
                d.VehiculoPlaca,
                d.TipoVehiculo,
                d.TipoCombustible,
                d.SalidaEn,
                d.DistanciaKm,
                db.FactoresEmision.Where(f => f.FactorEmisionId == d.FactorEmisionId).Select(f => (decimal?)f.FactorCo2Km).FirstOrDefault(),
                d.Co2Kg,
                d.Co2KgOperativo,
                d.Estado,
                d.MensajeError))
            .ToListAsync(cancelacion);

        var paginas = (int)Math.Ceiling(total / (double)filtro.Limit);
        return (datos, new MetaPaginacion(filtro.Page, filtro.Limit, total, paginas));
    }

    public async Task<(byte[] Contenido, string Nombre)> DescargarAsync(Guid id, int numero, CancellationToken cancelacion)
    {
        var version = await BuscarVersionAsync(id, numero, cancelacion);
        var contenido = await almacenamiento.LeerAsync(version.ClaveAlmacenamiento, cancelacion)
            ?? throw ApiException.NoEncontrado("El archivo de esta versión no está disponible en el almacenamiento.");

        var nombre = await db.ReportesViaje.AsNoTracking()
            .Where(r => r.ReporteViajeId == id)
            .Select(r => r.NombreArchivo)
            .FirstAsync(cancelacion);

        return (contenido, string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileNameWithoutExtension(nombre)}-v{numero}.csv"));
    }

    public async Task<VersionRespuesta> ReprocesarAsync(Guid id, int numero, Guid usuarioId, CancellationToken cancelacion)
    {
        var version = await BuscarVersionAsync(id, numero, cancelacion, seguimiento: true);
        if (version.EstadoCalculo != EstadoCalculo.Fallido)
        {
            throw ApiException.Conflicto("Solo se puede reprocesar una versión cuyo cálculo falló.");
        }

        version.EstadoCalculo = EstadoCalculo.Pendiente;
        version.MensajeError = null;
        version.CalculoIniciadoEn = null;
        version.CalculoTerminadoEn = null;
        await db.GuardarComoAsync(usuarioId, cancelacion);
        senal.Notificar();
        return AVersion(version);
    }

    private async Task<ReporteVersion> BuscarVersionAsync(
        Guid id, int numero, CancellationToken cancelacion, bool seguimiento = false)
    {
        var consulta = seguimiento ? db.ReporteVersiones : db.ReporteVersiones.AsNoTracking();
        return await consulta.FirstOrDefaultAsync(v => v.ReporteViajeId == id && v.NumeroVersion == numero, cancelacion)
            ?? throw ApiException.NoEncontrado("La versión del reporte no existe.");
    }

    private static VersionRespuesta AVersion(ReporteVersion v) => new(
        v.NumeroVersion,
        v.Origen,
        v.HashSha256,
        v.TamanoBytes,
        v.TotalFilas,
        v.EstadoCalculo,
        v.MensajeError,
        v.CalculoIniciadoEn,
        v.CalculoTerminadoEn,
        v.EsOficial,
        v.CreadoEn,
        v.Huella is { } h
            ? new HuellaResumen(h.FilasProcesadas, h.FilasValidas, h.FilasConError, h.DistanciaTotalKm,
                h.Co2TotalKg, Math.Round(h.Co2TotalKg / 1000m, 4), h.CalculadoEn)
            : null);

    private static EstadoDetalleHuella? Invalido(List<ErrorDetalle> errores)
    {
        errores.Add(new ErrorDetalle("estado", "Usa ok o error."));
        return null;
    }

    private static void ValidarPaginacion(int pagina, int limite, List<ErrorDetalle> errores)
    {
        if (pagina < 1)
        {
            errores.Add(new ErrorDetalle("page", "La página debe ser 1 o mayor."));
        }

        if (limite is < 1 or > LimiteMaximo)
        {
            errores.Add(new ErrorDetalle("limit", string.Create(CultureInfo.InvariantCulture, $"El límite debe estar entre 1 y {LimiteMaximo}.")));
        }
    }
}
