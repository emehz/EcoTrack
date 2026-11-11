using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using EcoTrack.Services.Reportes;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services;

public sealed partial class ViajeService(AppDbContext db)
{
    public const int LimiteMaximo = 100;
    public const int LimiteExportacion = 10000;
    private const int LargoMaximoLugar = 120;
    private const decimal DistanciaMaxima = 100000m;

    private static readonly TimeSpan HoraElSalvador = TimeSpan.FromHours(-6);

    private static readonly Dictionary<string, EstadoViaje> Estados = new()
    {
        ["planificado"] = EstadoViaje.Planificado,
        ["en_curso"] = EstadoViaje.EnCurso,
        ["completado"] = EstadoViaje.Completado,
        ["cancelado"] = EstadoViaje.Cancelado,
    };

    private static readonly Expression<Func<Viaje, ViajeRespuesta>> Proyeccion = v => new ViajeRespuesta(
        v.ViajeId,
        v.VehiculoId,
        v.Vehiculo!.Placa,
        v.ChoferId,
        v.Chofer == null ? null : v.Chofer.Nombres + " " + v.Chofer.Apellidos,
        v.Origen,
        v.Destino,
        v.SalidaEn,
        v.LlegadaEn,
        v.DistanciaKm,
        v.Co2Kg,
        v.Estado,
        v.CargaArchivoId);

    private static readonly Func<Viaje, ViajeRespuesta> ARespuesta = Proyeccion.Compile();

    public async Task<(IReadOnlyList<ViajeRespuesta> Datos, MetaViajes Meta)> ListarAsync(
        ViajeFiltro filtro, CancellationToken cancelacion)
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

        var (consulta, estado) = Filtrar(filtro, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var grupos = await consulta.GroupBy(v => v.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count(), Km = g.Sum(v => v.DistanciaKm), Co2 = g.Sum(v => v.Co2Kg) })
            .ToListAsync(cancelacion);
        int Contar(EstadoViaje e) => grupos.Find(g => g.Estado == e)?.Cantidad ?? 0;
        var conteos = new ConteosViaje(
            Contar(EstadoViaje.Planificado), Contar(EstadoViaje.EnCurso), Contar(EstadoViaje.Completado),
            Contar(EstadoViaje.Cancelado), grupos.Sum(g => g.Cantidad));

        var validos = grupos.Where(g => g.Estado != EstadoViaje.Cancelado && (estado is null || g.Estado == estado)).ToList();
        if (estado is not null)
        {
            consulta = consulta.Where(v => v.Estado == estado);
        }

        var total = estado is null ? conteos.Todos : Contar(estado.Value);
        var datos = await consulta
            .OrderByDescending(v => v.SalidaEn).ThenBy(v => v.ViajeId)
            .Skip((filtro.Page - 1) * filtro.Limit)
            .Take(filtro.Limit)
            .Select(Proyeccion)
            .ToListAsync(cancelacion);

        var paginas = (int)Math.Ceiling(total / (double)filtro.Limit);
        return (datos, new MetaViajes(
            filtro.Page, filtro.Limit, total, paginas, validos.Sum(g => g.Km), validos.Sum(g => g.Co2), conteos));
    }

    public async Task<(byte[] Contenido, string Nombre)> ExportarAsync(ViajeFiltro filtro, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var (consulta, estado) = Filtrar(filtro, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        if (estado is not null)
        {
            consulta = consulta.Where(v => v.Estado == estado);
        }

        if (await consulta.CountAsync(cancelacion) > LimiteExportacion)
        {
            throw ApiException.Validacion([new ErrorDetalle("desde", string.Create(CultureInfo.InvariantCulture,
                $"El listado tiene más de {LimiteExportacion} viajes. Acota el rango de fechas."))]);
        }

        var filas = await consulta
            .OrderByDescending(v => v.SalidaEn).ThenBy(v => v.ViajeId)
            .Select(v => new
            {
                v.ViajeId,
                v.Vehiculo!.Placa,
                v.Vehiculo.TipoVehiculo,
                v.Vehiculo.TipoCombustible,
                Licencia = v.Chofer == null ? null : v.Chofer.NumeroLicencia,
                v.Origen,
                v.Destino,
                v.SalidaEn,
                v.LlegadaEn,
                v.DistanciaKm,
                v.Estado,
                v.Co2Kg,
            })
            .ToListAsync(cancelacion);

        var contenido = ReporteCsv.Escribir(filas.Select(f => new FilaViajeReporte(
            f.ViajeId, f.Placa, Texto(f.TipoVehiculo), Texto(f.TipoCombustible), f.Licencia, f.Origen, f.Destino,
            f.SalidaEn, f.LlegadaEn, f.DistanciaKm, Texto(f.Estado), f.Co2Kg)));
        var hoy = DateTimeOffset.UtcNow.ToOffset(HoraElSalvador);
        return (contenido, string.Create(CultureInfo.InvariantCulture, $"viajes-{hoy:yyyyMMdd-HHmm}.csv"));
    }

    public async Task<ViajeDetalleRespuesta> ObtenerAsync(Guid id, CancellationToken cancelacion)
    {
        var viaje = await db.Viajes.AsNoTracking()
            .Include(v => v.Vehiculo)
            .Include(v => v.Chofer)
            .Include(v => v.FactorEmision)
            .Include(v => v.CreadoPor)
            .FirstOrDefaultAsync(v => v.ViajeId == id, cancelacion) ?? throw NoExiste();

        CargaDelViaje? carga = null;
        if (viaje.CargaArchivoId is { } cargaId)
        {
            carga = await db.Database.SqlQuery<CargaDelViaje>($"""
                SELECT carga_archivo_id AS "CargaArchivoId",
                       nombre_archivo_original AS "NombreArchivo",
                       creado_en AS "CreadoEn"
                  FROM carga_archivo
                 WHERE carga_archivo_id = {cargaId}
                """).FirstOrDefaultAsync(cancelacion);
        }

        var vehiculo = viaje.Vehiculo!;
        var factor = viaje.FactorEmision!;
        return new ViajeDetalleRespuesta(
            ARespuesta(viaje),
            new VehiculoDelViaje(vehiculo.VehiculoId, vehiculo.Placa, vehiculo.TipoVehiculo, vehiculo.TipoCombustible, vehiculo.Estado),
            viaje.Chofer is { } c ? new ChoferDelViaje(c.ChoferId, c.Nombres, c.Apellidos, c.NumeroLicencia, c.Estado) : null,
            new FactorDelViaje(factor.FactorEmisionId, factor.FactorCo2Km, factor.VigenteDesde, factor.VigenteHasta, factor.Fuente),
            carga,
            viaje.CreadoPor is { } u ? $"{u.Nombres} {u.Apellidos}" : null,
            viaje.CreadoEn);
    }

    public async Task<ViajeDetalleRespuesta> CrearAsync(ViajeGuardarSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var estado = EstadoViaje.EnCurso;
        if (!string.IsNullOrWhiteSpace(solicitud.Estado))
        {
            var leido = LeerEstado(solicitud.Estado, errores);
            if (leido == EstadoViaje.Cancelado)
            {
                errores.Add(new ErrorDetalle("estado", "Un viaje nuevo puede ser planificado, en_curso o completado."));
            }

            estado = leido ?? estado;
        }

        var datos = Validar(solicitud, estado, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        await ValidarRelacionesAsync(datos, null, errores, cancelacion);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        await ExigirSinDuplicadoAsync(datos.VehiculoId, datos.SalidaEn, null, cancelacion);

        var viaje = new Viaje
        {
            ViajeId = Guid.NewGuid(),
            VehiculoId = datos.VehiculoId,
            ChoferId = datos.ChoferId,
            Origen = datos.Origen,
            Destino = datos.Destino,
            SalidaEn = datos.SalidaEn,
            LlegadaEn = datos.LlegadaEn,
            DistanciaKm = datos.DistanciaKm,
            Estado = estado,
            CreadoPorUsuarioId = usuarioId,
        };
        db.Viajes.Add(viaje);
        await db.GuardarComoAsync(usuarioId, cancelacion);
        return await ObtenerAsync(viaje.ViajeId, cancelacion);
    }

    public async Task<ViajeDetalleRespuesta> ActualizarAsync(
        Guid id, ViajeGuardarSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var viaje = await db.Viajes.FirstOrDefaultAsync(v => v.ViajeId == id, cancelacion) ?? throw NoExiste();
        if (viaje.Estado == EstadoViaje.Cancelado)
        {
            throw ApiException.Conflicto("Un viaje cancelado no se puede editar.");
        }

        var errores = new List<ErrorDetalle>();
        var datos = Validar(solicitud, viaje.Estado, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        await ValidarRelacionesAsync(datos, viaje, errores, cancelacion);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        await ExigirSinDuplicadoAsync(datos.VehiculoId, datos.SalidaEn, id, cancelacion);

        var recalcularFactor = viaje.VehiculoId != datos.VehiculoId || viaje.SalidaEn != datos.SalidaEn;
        viaje.VehiculoId = datos.VehiculoId;
        viaje.ChoferId = datos.ChoferId;
        viaje.Origen = datos.Origen;
        viaje.Destino = datos.Destino;
        viaje.SalidaEn = datos.SalidaEn;
        viaje.LlegadaEn = datos.LlegadaEn;
        viaje.DistanciaKm = datos.DistanciaKm;
        if (recalcularFactor)
        {
            viaje.FactorEmisionId = null;
        }

        await db.GuardarComoAsync(usuarioId, cancelacion);
        return await ObtenerAsync(id, cancelacion);
    }

    public async Task<ViajeDetalleRespuesta> CambiarEstadoAsync(
        Guid id, ViajeEstadoSolicitud solicitud, Guid usuarioId, bool esAdmin, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var nuevo = LeerEstado(solicitud.Estado, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var viaje = await db.Viajes.FirstOrDefaultAsync(v => v.ViajeId == id, cancelacion) ?? throw NoExiste();
        var actual = viaje.Estado;
        if (actual == nuevo)
        {
            return await ObtenerAsync(id, cancelacion);
        }

        switch (actual, nuevo)
        {
            case (EstadoViaje.Planificado, EstadoViaje.EnCurso):
                break;
            case (EstadoViaje.EnCurso, EstadoViaje.Completado):
                if (solicitud.LlegadaEn is not { } llegada)
                {
                    throw ApiException.Validacion([new ErrorDetalle("llegada_en", "Indica la fecha y hora de llegada.")]);
                }

                if (llegada < viaje.SalidaEn)
                {
                    throw ApiException.Validacion([new ErrorDetalle("llegada_en", "La llegada no puede ser anterior a la salida.")]);
                }

                viaje.LlegadaEn = llegada.ToUniversalTime();
                break;
            case (EstadoViaje.Planificado or EstadoViaje.EnCurso, EstadoViaje.Cancelado):
                break;
            case (EstadoViaje.Completado, EstadoViaje.EnCurso):
                if (!esAdmin)
                {
                    throw new ApiException(StatusCodes.Status403Forbidden, CodigosError.SinPermiso,
                        "Solo un administrador puede reabrir un viaje completado.");
                }

                viaje.LlegadaEn = null;
                break;
            default:
                throw ApiException.Conflicto(string.Create(CultureInfo.InvariantCulture,
                    $"Un viaje {Texto(actual)} no puede pasar a {Texto(nuevo!.Value)}."));
        }

        viaje.Estado = nuevo!.Value;
        await db.GuardarComoAsync(usuarioId, cancelacion);
        return await ObtenerAsync(id, cancelacion);
    }

    public async Task EliminarAsync(Guid id, bool confirmar, Guid usuarioId, CancellationToken cancelacion)
    {
        if (!confirmar)
        {
            throw ApiException.Validacion([new ErrorDetalle("confirmar", "Confirma que quieres eliminar el viaje de forma definitiva.")]);
        }

        var viaje = await db.Viajes.FirstOrDefaultAsync(v => v.ViajeId == id, cancelacion) ?? throw NoExiste();
        db.Viajes.Remove(viaje);
        await db.GuardarComoAsync(usuarioId, cancelacion);
    }

    private (IQueryable<Viaje> Consulta, EstadoViaje? Estado) Filtrar(ViajeFiltro filtro, List<ErrorDetalle> errores)
    {
        EstadoViaje? estado = null;
        if (!string.IsNullOrWhiteSpace(filtro.Estado) && !string.Equals(filtro.Estado.Trim(), "todos", StringComparison.OrdinalIgnoreCase))
        {
            estado = LeerEstado(filtro.Estado, errores);
        }

        if (filtro.Desde is { } d && filtro.Hasta is { } h && d > h)
        {
            errores.Add(new ErrorDetalle("hasta", "La fecha final no puede ser anterior a la inicial."));
        }

        var consulta = db.Viajes.AsNoTracking();
        if (filtro.Desde is { } desde)
        {
            var inicio = InicioDelDia(desde);
            consulta = consulta.Where(v => v.SalidaEn >= inicio);
        }

        if (filtro.Hasta is { } hasta)
        {
            var fin = InicioDelDia(hasta.AddDays(1));
            consulta = consulta.Where(v => v.SalidaEn < fin);
        }

        if (filtro.VehiculoId is { } vehiculoId)
        {
            consulta = consulta.Where(v => v.VehiculoId == vehiculoId);
        }

        if (filtro.ChoferId is { } choferId)
        {
            consulta = consulta.Where(v => v.ChoferId == choferId);
        }

        if (filtro.CargaArchivoId is { } cargaId)
        {
            consulta = consulta.Where(v => v.CargaArchivoId == cargaId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Q))
        {
            var patron = Busqueda.Patron(filtro.Q);
            consulta = consulta.Where(v =>
                Regex.IsMatch(v.Origen + " " + v.Destino, patron, RegexOptions.IgnoreCase)
                || Regex.IsMatch(v.Vehiculo!.Placa, patron, RegexOptions.IgnoreCase));
        }

        return (consulta, estado);
    }

    private sealed record DatosViaje(
        Guid VehiculoId, Guid? ChoferId, string Origen, string Destino, DateTimeOffset SalidaEn, DateTimeOffset? LlegadaEn, decimal DistanciaKm);

    private static DatosViaje Validar(ViajeGuardarSolicitud solicitud, EstadoViaje estado, List<ErrorDetalle> errores)
    {
        if (solicitud.VehiculoId is null)
        {
            errores.Add(new ErrorDetalle("vehiculo_id", "Elige el vehículo."));
        }

        var origen = Lugar(solicitud.Origen, "origen", "Escribe el origen.", errores);
        var destino = Lugar(solicitud.Destino, "destino", "Escribe el destino.", errores);

        if (solicitud.SalidaEn is null)
        {
            errores.Add(new ErrorDetalle("salida_en", "Indica la fecha y hora de salida."));
        }

        if (solicitud.DistanciaKm is not { } distancia)
        {
            errores.Add(new ErrorDetalle("distancia_km", "Indica la distancia en kilómetros."));
        }
        else if (distancia is < 0 or > DistanciaMaxima)
        {
            errores.Add(new ErrorDetalle("distancia_km", "La distancia debe estar entre 0 y 100 000 km."));
        }

        if (estado == EstadoViaje.Completado)
        {
            if (solicitud.LlegadaEn is null)
            {
                errores.Add(new ErrorDetalle("llegada_en", "Un viaje completado necesita la fecha y hora de llegada."));
            }
            else if (solicitud.SalidaEn is { } salida && solicitud.LlegadaEn < salida)
            {
                errores.Add(new ErrorDetalle("llegada_en", "La llegada no puede ser anterior a la salida."));
            }
        }
        else if (solicitud.LlegadaEn is not null)
        {
            errores.Add(new ErrorDetalle("llegada_en", "La llegada se registra al completar el viaje."));
        }

        return new DatosViaje(
            solicitud.VehiculoId ?? Guid.Empty,
            solicitud.ChoferId,
            origen,
            destino,
            solicitud.SalidaEn?.ToUniversalTime() ?? default,
            estado == EstadoViaje.Completado ? solicitud.LlegadaEn?.ToUniversalTime() : null,
            Math.Round(solicitud.DistanciaKm ?? 0, 2, MidpointRounding.AwayFromZero));
    }

    private async Task ValidarRelacionesAsync(
        DatosViaje datos, Viaje? anterior, List<ErrorDetalle> errores, CancellationToken cancelacion)
    {
        var vehiculo = await db.Vehiculos.AsNoTracking().FirstOrDefaultAsync(v => v.VehiculoId == datos.VehiculoId, cancelacion);
        if (vehiculo is null)
        {
            errores.Add(new ErrorDetalle("vehiculo_id", "El vehículo no existe."));
        }
        else
        {
            if (vehiculo.Estado != EstadoVehiculo.Activo && anterior?.VehiculoId != datos.VehiculoId)
            {
                errores.Add(new ErrorDetalle("vehiculo_id", "El vehículo no está activo. Elige uno disponible."));
            }

            var fecha = DateOnly.FromDateTime(datos.SalidaEn.ToOffset(HoraElSalvador).DateTime);
            var hayFactor = await db.FactoresEmision.AnyAsync(f =>
                f.TipoCombustible == vehiculo.TipoCombustible
                && f.TipoVehiculo == vehiculo.TipoVehiculo
                && f.VigenteDesde <= fecha
                && (f.VigenteHasta == null || fecha < f.VigenteHasta), cancelacion);
            if (!hayFactor)
            {
                errores.Add(new ErrorDetalle("salida_en", "No hay un factor de emisión vigente para este vehículo en esa fecha."));
            }
        }

        if (datos.ChoferId is { } choferId)
        {
            var chofer = await db.Choferes.AsNoTracking().FirstOrDefaultAsync(c => c.ChoferId == choferId, cancelacion);
            if (chofer is null)
            {
                errores.Add(new ErrorDetalle("chofer_id", "El chofer no existe."));
            }
            else if (chofer.Estado != EstadoChofer.Activo && anterior?.ChoferId != choferId)
            {
                errores.Add(new ErrorDetalle("chofer_id", "El chofer no está activo y no puede asignarse a viajes."));
            }
        }
    }

    private async Task ExigirSinDuplicadoAsync(Guid vehiculoId, DateTimeOffset salida, Guid? excepto, CancellationToken cancelacion)
    {
        var existente = await db.Viajes.AsNoTracking()
            .Where(v => v.VehiculoId == vehiculoId && v.SalidaEn == salida && v.Estado != EstadoViaje.Cancelado && v.ViajeId != excepto)
            .Select(v => (Guid?)v.ViajeId)
            .FirstOrDefaultAsync(cancelacion);
        if (existente is { } viajeId)
        {
            throw ApiException.Conflicto("Ya hay un viaje de este vehículo con la misma fecha y hora de salida.",
            [
                new ErrorDetalle("salida_en", "Este vehículo ya tiene un viaje que sale a esa hora."),
                new ErrorDetalle("viaje_existente", viajeId.ToString()),
            ]);
        }
    }

    private static string Lugar(string? valor, string campo, string mensaje, List<ErrorDetalle> errores)
    {
        var texto = string.IsNullOrWhiteSpace(valor) ? null : MultiplesEspacios().Replace(valor.Trim(), " ");
        if (texto is null)
        {
            errores.Add(new ErrorDetalle(campo, mensaje));
        }
        else if (texto.Length > LargoMaximoLugar)
        {
            errores.Add(new ErrorDetalle(campo, "Usa como máximo 120 caracteres."));
        }

        return texto ?? string.Empty;
    }

    private static EstadoViaje? LeerEstado(string? valor, List<ErrorDetalle> errores)
    {
        if (valor is not null && Estados.TryGetValue(valor.Trim().ToLowerInvariant(), out var estado))
        {
            return estado;
        }

        errores.Add(new ErrorDetalle("estado", "Elige planificado, en_curso, completado o cancelado."));
        return null;
    }

    private static DateTimeOffset InicioDelDia(DateOnly fecha) =>
        new DateTimeOffset(fecha.ToDateTime(TimeOnly.MinValue), HoraElSalvador).ToUniversalTime();

    private static string Texto<T>(T valor) where T : struct, Enum =>
        System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(valor.ToString());

    private static ApiException NoExiste() => ApiException.NoEncontrado("El viaje no existe.");

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiplesEspacios();
}
