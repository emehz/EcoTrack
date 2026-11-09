using System.Globalization;
using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services.Auditorias;

public sealed class AuditoriaReporteService(AppDbContext db, ControlesAuditoria controles)
{
    public const int LimiteMaximo = 100;

    private static readonly Dictionary<string, SeveridadHallazgo> Severidades = new()
    {
        ["critica"] = SeveridadHallazgo.Critica,
        ["alta"] = SeveridadHallazgo.Alta,
        ["media"] = SeveridadHallazgo.Media,
        ["baja"] = SeveridadHallazgo.Baja,
        ["observacion"] = SeveridadHallazgo.Observacion,
    };

    private static readonly Dictionary<string, EstadoHallazgo> EstadosHallazgo = new()
    {
        ["abierto"] = EstadoHallazgo.Abierto,
        ["corregido"] = EstadoHallazgo.Corregido,
        ["riesgo_aceptado"] = EstadoHallazgo.RiesgoAceptado,
        ["descartado"] = EstadoHallazgo.Descartado,
    };

    private static readonly Dictionary<string, ResultadoAuditoria> Resultados = new()
    {
        ["aprobado"] = ResultadoAuditoria.Aprobado,
        ["aprobado_con_ajustes"] = ResultadoAuditoria.AprobadoConAjustes,
        ["rechazado"] = ResultadoAuditoria.Rechazado,
    };

    private static readonly Dictionary<string, EstadoAuditoria> EstadosAuditoria = new()
    {
        ["en_revision"] = EstadoAuditoria.EnRevision,
        ["cerrada"] = EstadoAuditoria.Cerrada,
    };

    public async Task<AuditoriaDetalleRespuesta> IniciarAsync(
        Guid reporteId, AuditoriaIniciarSolicitud solicitud, Guid usuarioId, bool esAdmin, CancellationToken cancelacion)
    {
        var reporte = await db.ReportesViaje.FirstOrDefaultAsync(r => r.ReporteViajeId == reporteId, cancelacion)
            ?? throw ApiException.NoEncontrado("El reporte no existe.");

        var version = await db.ReporteVersiones
            .Include(v => v.Huella!).ThenInclude(h => h.Detalle)
            .Where(v => v.ReporteViajeId == reporteId && v.EstadoCalculo == EstadoCalculo.Completado)
            .OrderByDescending(v => v.NumeroVersion)
            .FirstOrDefaultAsync(cancelacion)
            ?? throw ApiException.Validacion([new ErrorDetalle("reporte", "El reporte todavía no tiene un cálculo de huella completado.")]);

        if (await db.AuditoriasReporte.AnyAsync(a => a.ReporteViajeId == reporteId && a.Estado == EstadoAuditoria.EnRevision, cancelacion))
        {
            throw ApiException.Conflicto("Ya hay una auditoría abierta para este reporte.");
        }

        var motivo = Texto(solicitud.MotivoCiclo);
        if (await db.AuditoriasReporte.AnyAsync(a => a.ReporteViajeId == reporteId, cancelacion))
        {
            if (!esAdmin)
            {
                throw SinPermiso("Solo un administrador puede abrir un nuevo ciclo de auditoría.");
            }

            if (motivo is null)
            {
                throw ApiException.Validacion([new ErrorDetalle("motivo_ciclo", "Indica el motivo para abrir un nuevo ciclo de auditoría.")]);
            }
        }

        var auditoria = new AuditoriaReporte
        {
            AuditoriaReporteId = Guid.NewGuid(),
            ReporteViajeId = reporteId,
            VersionAuditadaId = version.ReporteVersionId,
            AuditorUsuarioId = usuarioId,
            Estado = EstadoAuditoria.EnRevision,
            Alcance = Texto(solicitud.Alcance),
            MotivoCiclo = motivo,
            IniciadoEn = DateTimeOffset.UtcNow,
        };
        auditoria.Hallazgos.AddRange(await controles.EjecutarAsync(auditoria, reporte, version, cancelacion));

        db.AuditoriasReporte.Add(auditoria);
        await db.GuardarComoAsync(usuarioId, cancelacion);
        return await ObtenerAsync(auditoria.AuditoriaReporteId, cancelacion);
    }

    public async Task<(IReadOnlyList<AuditoriaResumenRespuesta> Datos, MetaPaginacion Meta)> ListarAsync(
        AuditoriaFiltro filtro, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var estado = LeerOpcional(EstadosAuditoria, filtro.Estado, "estado", "Usa en_revision o cerrada.", errores);
        var resultado = LeerOpcional(Resultados, filtro.Resultado, "resultado", "Usa aprobado, aprobado_con_ajustes o rechazado.", errores);
        ValidarPaginacion(filtro.Page, filtro.Limit, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var consulta = db.AuditoriasReporte.AsNoTracking();
        if (estado is not null)
        {
            consulta = consulta.Where(a => a.Estado == estado);
        }

        if (resultado is not null)
        {
            consulta = consulta.Where(a => a.Resultado == resultado);
        }

        if (filtro.ReporteViajeId is { } reporteId)
        {
            consulta = consulta.Where(a => a.ReporteViajeId == reporteId);
        }

        var total = await consulta.CountAsync(cancelacion);
        var datos = await consulta
            .OrderByDescending(a => a.IniciadoEn)
            .Skip((filtro.Page - 1) * filtro.Limit)
            .Take(filtro.Limit)
            .Select(a => new AuditoriaResumenRespuesta(
                a.AuditoriaReporteId,
                a.ReporteViajeId,
                a.Reporte!.NombreArchivo,
                a.VersionAuditada!.NumeroVersion,
                a.Auditor!.Nombres + " " + a.Auditor.Apellidos,
                a.Estado,
                a.Resultado,
                a.IniciadoEn,
                a.CerradoEn,
                a.Hallazgos.Count(h => h.Estado == EstadoHallazgo.Abierto),
                a.Hallazgos.Count(h => h.Estado == EstadoHallazgo.Abierto
                    && (h.Severidad == SeveridadHallazgo.Critica || h.Severidad == SeveridadHallazgo.Alta))))
            .ToListAsync(cancelacion);

        var paginas = (int)Math.Ceiling(total / (double)filtro.Limit);
        return (datos, new MetaPaginacion(filtro.Page, filtro.Limit, total, paginas));
    }

    public async Task<AuditoriaDetalleRespuesta> ObtenerAsync(Guid id, CancellationToken cancelacion)
    {
        var auditoria = await db.AuditoriasReporte.AsNoTracking()
            .Include(a => a.Reporte)
            .Include(a => a.VersionAuditada)
            .Include(a => a.VersionFinal)
            .Include(a => a.Auditor)
            .Include(a => a.Hallazgos).ThenInclude(h => h.ResueltoPor)
            .Include(a => a.Hallazgos).ThenInclude(h => h.DetectadoPor)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.AuditoriaReporteId == id, cancelacion)
            ?? throw ApiException.NoEncontrado("La auditoría no existe.");

        var hallazgos = auditoria.Hallazgos
            .OrderBy(h => h.Estado == EstadoHallazgo.Abierto ? 0 : 1)
            .ThenBy(h => h.Severidad)
            .ThenBy(h => h.FilaNumero ?? 0)
            .Select(AHallazgo)
            .ToList();

        return new AuditoriaDetalleRespuesta(
            auditoria.AuditoriaReporteId,
            auditoria.ReporteViajeId,
            auditoria.Reporte!.NombreArchivo,
            auditoria.Reporte.PeriodoDesde,
            auditoria.Reporte.PeriodoHasta,
            auditoria.Reporte.Estado,
            AVersion(auditoria.VersionAuditada!),
            auditoria.VersionFinal is null ? null : AVersion(auditoria.VersionFinal),
            NombreDe(auditoria.Auditor) ?? string.Empty,
            auditoria.Estado,
            auditoria.Resultado,
            auditoria.Alcance,
            auditoria.Conclusion,
            auditoria.MotivoCiclo,
            auditoria.IniciadoEn,
            auditoria.CerradoEn,
            hallazgos);
    }

    public async Task<HallazgoRespuesta> RegistrarHallazgoAsync(
        Guid auditoriaId, HallazgoCrearSolicitud solicitud, Guid usuarioId, bool esAdmin, CancellationToken cancelacion)
    {
        var auditoria = await BuscarAbiertaAsync(auditoriaId, usuarioId, esAdmin, cancelacion);

        var errores = new List<ErrorDetalle>();
        var severidad = LeerOpcional(Severidades, solicitud.Severidad, "severidad",
            "Elige critica, alta, media, baja u observacion.", errores);
        if (severidad is null && errores.Count == 0)
        {
            errores.Add(new ErrorDetalle("severidad", "Elige la severidad del hallazgo."));
        }

        var descripcion = Texto(solicitud.Descripcion);
        if (descripcion is null)
        {
            errores.Add(new ErrorDetalle("descripcion", "Describe el hallazgo."));
        }

        if (solicitud.FilaNumero is < 2)
        {
            errores.Add(new ErrorDetalle("fila_numero", "La fila debe ser 2 o mayor; la fila 1 es el encabezado."));
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var hallazgo = new HallazgoAuditoria
        {
            HallazgoAuditoriaId = Guid.NewGuid(),
            AuditoriaReporteId = auditoria.AuditoriaReporteId,
            VersionOrigenId = auditoria.VersionAuditadaId,
            Origen = OrigenHallazgo.Manual,
            Severidad = severidad!.Value,
            FilaNumero = solicitud.FilaNumero,
            Campo = Texto(solicitud.Campo),
            Descripcion = descripcion!,
            ValorEncontrado = Texto(solicitud.ValorEncontrado),
            ValorEsperado = Texto(solicitud.ValorEsperado),
            Estado = EstadoHallazgo.Abierto,
            DetectadoPorUsuarioId = usuarioId,
            DetectadoEn = DateTimeOffset.UtcNow,
        };
        db.HallazgosAuditoria.Add(hallazgo);
        await db.GuardarComoAsync(usuarioId, cancelacion);
        return await ObtenerHallazgoAsync(hallazgo.HallazgoAuditoriaId, cancelacion);
    }

    public async Task<HallazgoRespuesta> CambiarEstadoHallazgoAsync(
        Guid auditoriaId, Guid hallazgoId, HallazgoEstadoSolicitud solicitud, Guid usuarioId, bool esAdmin, CancellationToken cancelacion)
    {
        var auditoria = await BuscarAbiertaAsync(auditoriaId, usuarioId, esAdmin, cancelacion);
        var hallazgo = await db.HallazgosAuditoria
            .FirstOrDefaultAsync(h => h.HallazgoAuditoriaId == hallazgoId && h.AuditoriaReporteId == auditoriaId, cancelacion)
            ?? throw ApiException.NoEncontrado("El hallazgo no existe en esta auditoría.");

        var errores = new List<ErrorDetalle>();
        var estado = LeerOpcional(EstadosHallazgo, solicitud.Estado, "estado",
            "Elige abierto, corregido, riesgo_aceptado o descartado.", errores);
        if (estado is null && errores.Count == 0)
        {
            errores.Add(new ErrorDetalle("estado", "Indica el nuevo estado del hallazgo."));
        }

        var justificacion = Texto(solicitud.Justificacion);
        if (estado is not null and not EstadoHallazgo.Abierto && justificacion is null)
        {
            errores.Add(new ErrorDetalle("justificacion", "Explica por qué cambias el estado del hallazgo."));
        }

        Guid? versionCorreccion = null;
        if (estado == EstadoHallazgo.Corregido)
        {
            versionCorreccion = solicitud.VersionCorreccion is { } numero
                ? await db.ReporteVersiones.AsNoTracking()
                    .Where(v => v.ReporteViajeId == auditoria.ReporteViajeId && v.NumeroVersion == numero)
                    .Select(v => (Guid?)v.ReporteVersionId)
                    .FirstOrDefaultAsync(cancelacion)
                : null;

            if (versionCorreccion is null)
            {
                errores.Add(new ErrorDetalle("version_correccion", "Indica una versión del reporte que contenga la corrección."));
            }
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        if (estado == EstadoHallazgo.RiesgoAceptado && !esAdmin)
        {
            throw SinPermiso("Solo un administrador puede aceptar el riesgo de un hallazgo.");
        }

        hallazgo.Estado = estado!.Value;
        if (estado == EstadoHallazgo.Abierto)
        {
            hallazgo.Justificacion = null;
            hallazgo.ResueltoPorUsuarioId = null;
            hallazgo.ResueltoEn = null;
            hallazgo.VersionCorreccionId = null;
        }
        else
        {
            hallazgo.Justificacion = justificacion;
            hallazgo.ResueltoPorUsuarioId = usuarioId;
            hallazgo.ResueltoEn = DateTimeOffset.UtcNow;
            hallazgo.VersionCorreccionId = versionCorreccion;
        }

        await db.GuardarComoAsync(usuarioId, cancelacion);
        return await ObtenerHallazgoAsync(hallazgo.HallazgoAuditoriaId, cancelacion);
    }

    public async Task<AuditoriaDetalleRespuesta> CerrarAsync(
        Guid auditoriaId, AuditoriaCerrarSolicitud solicitud, Guid usuarioId, bool esAdmin, CancellationToken cancelacion)
    {
        var auditoria = await BuscarAbiertaAsync(auditoriaId, usuarioId, esAdmin, cancelacion);

        var errores = new List<ErrorDetalle>();
        var resultado = LeerOpcional(Resultados, solicitud.Resultado, "resultado",
            "Elige aprobado, aprobado_con_ajustes o rechazado.", errores);
        if (resultado is null && errores.Count == 0)
        {
            errores.Add(new ErrorDetalle("resultado", "Indica el resultado de la auditoría."));
        }

        var conclusion = Texto(solicitud.Conclusion);
        if (conclusion is null)
        {
            errores.Add(new ErrorDetalle("conclusion", "Escribe la conclusión de la auditoría."));
        }

        Guid? versionFinal = resultado == ResultadoAuditoria.Rechazado ? null : auditoria.VersionAuditadaId;
        if (solicitud.VersionFinal is { } numero)
        {
            versionFinal = await db.ReporteVersiones.AsNoTracking()
                .Where(v => v.ReporteViajeId == auditoria.ReporteViajeId && v.NumeroVersion == numero)
                .Select(v => (Guid?)v.ReporteVersionId)
                .FirstOrDefaultAsync(cancelacion);

            if (versionFinal is null)
            {
                errores.Add(new ErrorDetalle("version_final", "La versión final no existe en este reporte."));
            }
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        auditoria.Estado = EstadoAuditoria.Cerrada;
        auditoria.Resultado = resultado;
        auditoria.Conclusion = conclusion;
        auditoria.VersionFinalId = versionFinal;
        auditoria.CerradoEn = DateTimeOffset.UtcNow;
        await db.GuardarComoAsync(usuarioId, cancelacion);
        return await ObtenerAsync(auditoria.AuditoriaReporteId, cancelacion);
    }

    private async Task<AuditoriaReporte> BuscarAbiertaAsync(Guid id, Guid usuarioId, bool esAdmin, CancellationToken cancelacion)
    {
        var auditoria = await db.AuditoriasReporte.FirstOrDefaultAsync(a => a.AuditoriaReporteId == id, cancelacion)
            ?? throw ApiException.NoEncontrado("La auditoría no existe.");

        if (auditoria.Estado == EstadoAuditoria.Cerrada)
        {
            throw ApiException.Conflicto("La auditoría está cerrada y ya no se puede modificar.");
        }

        if (!esAdmin && auditoria.AuditorUsuarioId != usuarioId)
        {
            throw SinPermiso("Solo el auditor que inició esta auditoría o un administrador pueden modificarla.");
        }

        return auditoria;
    }

    private async Task<HallazgoRespuesta> ObtenerHallazgoAsync(Guid id, CancellationToken cancelacion)
    {
        var hallazgo = await db.HallazgosAuditoria.AsNoTracking()
            .Include(h => h.ResueltoPor)
            .Include(h => h.DetectadoPor)
            .FirstAsync(h => h.HallazgoAuditoriaId == id, cancelacion);
        return AHallazgo(hallazgo);
    }

    private static HallazgoRespuesta AHallazgo(HallazgoAuditoria h) => new(
        h.HallazgoAuditoriaId,
        h.Origen,
        h.Control,
        h.Severidad,
        h.FilaNumero,
        h.Campo,
        h.Descripcion,
        h.ValorEncontrado,
        h.ValorEsperado,
        h.Estado,
        h.Justificacion,
        NombreDe(h.ResueltoPor),
        h.ResueltoEn,
        NombreDe(h.DetectadoPor),
        h.DetectadoEn);

    private static VersionAuditadaRespuesta AVersion(ReporteVersion v) => new(v.NumeroVersion, v.HashSha256, v.EstadoCalculo, v.EsOficial);

    private static string? NombreDe(Usuario? usuario) => usuario is null ? null : $"{usuario.Nombres} {usuario.Apellidos}";

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static ApiException SinPermiso(string mensaje) =>
        new(StatusCodes.Status403Forbidden, CodigosError.SinPermiso, mensaje);

    private static T? LeerOpcional<T>(
        Dictionary<string, T> catalogo, string? valor, string campo, string mensaje, List<ErrorDetalle> errores)
        where T : struct
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (catalogo.TryGetValue(valor.Trim().ToLowerInvariant(), out var encontrado))
        {
            return encontrado;
        }

        errores.Add(new ErrorDetalle(campo, mensaje));
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
