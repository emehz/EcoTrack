using System.Globalization;
using System.Linq.Expressions;
using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services;

public sealed class CombustibleService(AppDbContext db, TimeProvider reloj)
{
    public const int LimiteMaximo = 100;
    private const decimal LitrosMaximos = 2000m;
    private const decimal PrecioMaximo = 100m;
    private static readonly TimeSpan HoraElSalvador = TimeSpan.FromHours(-6);
    private static readonly TimeSpan MargenFuturo = TimeSpan.FromMinutes(10);

    private static readonly Expression<Func<CargaCombustible, CargaRespuesta>> Proyeccion = c => new CargaRespuesta(
        c.CargaCombustibleId,
        c.VehiculoId,
        c.Vehiculo!.Placa,
        c.Vehiculo.TipoCombustible,
        c.CargadoEn,
        c.Litros,
        c.PrecioPorLitro,
        c.PrecioPorLitro == null ? null : Math.Round(c.Litros * c.PrecioPorLitro.Value, 2),
        c.ViajeId,
        c.Viaje == null ? null : c.Viaje.Origen + " › " + c.Viaje.Destino);

    public async Task<(IReadOnlyList<CargaRespuesta> Datos, MetaCargas Meta)> ListarAsync(
        CargaFiltro filtro, CancellationToken cancelacion)
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

        if (filtro.Desde is { } d && filtro.Hasta is { } h && d > h)
        {
            errores.Add(new ErrorDetalle("hasta", "La fecha final no puede ser anterior a la inicial."));
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var consulta = db.CargasCombustible.AsNoTracking();
        if (filtro.VehiculoId is { } vehiculoId)
        {
            consulta = consulta.Where(c => c.VehiculoId == vehiculoId);
        }

        if (filtro.Desde is { } desde)
        {
            var inicio = InicioDelDia(desde);
            consulta = consulta.Where(c => c.CargadoEn >= inicio);
        }

        if (filtro.Hasta is { } hasta)
        {
            var fin = InicioDelDia(hasta.AddDays(1));
            consulta = consulta.Where(c => c.CargadoEn < fin);
        }

        var totales = await consulta
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Cantidad = g.Count(),
                Litros = g.Sum(c => c.Litros),
                Costo = g.Sum(c => c.PrecioPorLitro == null ? 0 : c.Litros * c.PrecioPorLitro.Value),
            })
            .FirstOrDefaultAsync(cancelacion);

        var datos = await consulta
            .OrderByDescending(c => c.CargadoEn).ThenBy(c => c.CargaCombustibleId)
            .Skip((filtro.Page - 1) * filtro.Limit)
            .Take(filtro.Limit)
            .Select(Proyeccion)
            .ToListAsync(cancelacion);

        var total = totales?.Cantidad ?? 0;
        return (datos, new MetaCargas(
            filtro.Page, filtro.Limit, total, (int)Math.Ceiling(total / (double)filtro.Limit),
            totales?.Litros ?? 0, Math.Round(totales?.Costo ?? 0, 2)));
    }

    public async Task<CargaRespuesta> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        await db.CargasCombustible.AsNoTracking()
            .Where(c => c.CargaCombustibleId == id)
            .Select(Proyeccion)
            .FirstOrDefaultAsync(cancelacion)
        ?? throw NoExiste();

    public async Task<CargaRespuesta> CrearAsync(CargaGuardarSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var datos = await ValidarAsync(solicitud, cancelacion);
        var carga = new CargaCombustible
        {
            CargaCombustibleId = Guid.NewGuid(),
            VehiculoId = datos.VehiculoId,
            CargadoEn = datos.CargadoEn,
            Litros = datos.Litros,
            PrecioPorLitro = datos.Precio,
            ViajeId = datos.ViajeId,
            CreadoPorUsuarioId = usuarioId,
        };
        db.CargasCombustible.Add(carga);
        await db.GuardarComoAsync(usuarioId, cancelacion);
        return await ObtenerAsync(carga.CargaCombustibleId, cancelacion);
    }

    public async Task<CargaRespuesta> ActualizarAsync(
        Guid id, CargaGuardarSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var carga = await db.CargasCombustible.FirstOrDefaultAsync(c => c.CargaCombustibleId == id, cancelacion) ?? throw NoExiste();
        var datos = await ValidarAsync(solicitud, cancelacion);
        carga.VehiculoId = datos.VehiculoId;
        carga.CargadoEn = datos.CargadoEn;
        carga.Litros = datos.Litros;
        carga.PrecioPorLitro = datos.Precio;
        carga.ViajeId = datos.ViajeId;
        await db.GuardarComoAsync(usuarioId, cancelacion);
        return await ObtenerAsync(id, cancelacion);
    }

    public async Task EliminarAsync(Guid id, bool confirmar, Guid usuarioId, CancellationToken cancelacion)
    {
        if (!confirmar)
        {
            throw ApiException.Validacion([new ErrorDetalle("confirmar", "Confirma que quieres eliminar la carga de forma definitiva.")]);
        }

        var carga = await db.CargasCombustible.FirstOrDefaultAsync(c => c.CargaCombustibleId == id, cancelacion) ?? throw NoExiste();
        db.CargasCombustible.Remove(carga);
        await db.GuardarComoAsync(usuarioId, cancelacion);
    }

    private sealed record DatosCarga(Guid VehiculoId, DateTimeOffset CargadoEn, decimal Litros, decimal? Precio, Guid? ViajeId);

    private async Task<DatosCarga> ValidarAsync(CargaGuardarSolicitud solicitud, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        if (solicitud.VehiculoId is null)
        {
            errores.Add(new ErrorDetalle("vehiculo_id", "Elige el vehículo."));
        }

        if (solicitud.CargadoEn is not { } cargadoEn)
        {
            errores.Add(new ErrorDetalle("cargado_en", "Indica la fecha y hora de la carga."));
        }
        else if (cargadoEn > reloj.GetUtcNow() + MargenFuturo)
        {
            errores.Add(new ErrorDetalle("cargado_en", "La carga no puede tener una fecha futura."));
        }

        if (solicitud.Litros is not { } litros || litros <= 0)
        {
            errores.Add(new ErrorDetalle("litros", "Escribe una cantidad mayor que cero."));
        }
        else if (litros > LitrosMaximos)
        {
            errores.Add(new ErrorDetalle("litros", "La cantidad no puede pasar de 2 000 litros en una carga."));
        }

        if (solicitud.PrecioPorLitro is { } precio && precio is < 0 or > PrecioMaximo)
        {
            errores.Add(new ErrorDetalle("precio_por_litro", "El precio por litro debe estar entre 0 y 100."));
        }

        if (solicitud.VehiculoId is { } vehiculoId)
        {
            var vehiculo = await db.Vehiculos.AsNoTracking().FirstOrDefaultAsync(v => v.VehiculoId == vehiculoId, cancelacion);
            if (vehiculo is null)
            {
                errores.Add(new ErrorDetalle("vehiculo_id", "El vehículo no existe."));
            }
            else if (vehiculo.TipoCombustible == TipoCombustible.Electrico)
            {
                errores.Add(new ErrorDetalle("vehiculo_id", "Los vehículos eléctricos no registran cargas de combustible."));
            }

            if (solicitud.ViajeId is { } viajeId)
            {
                var deEsteVehiculo = await db.Viajes.AnyAsync(v => v.ViajeId == viajeId && v.VehiculoId == vehiculoId, cancelacion);
                if (!deEsteVehiculo)
                {
                    errores.Add(new ErrorDetalle("viaje_id", "El viaje no pertenece a este vehículo."));
                }
            }
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        return new DatosCarga(
            solicitud.VehiculoId!.Value,
            solicitud.CargadoEn!.Value.ToUniversalTime(),
            Math.Round(solicitud.Litros!.Value, 2, MidpointRounding.AwayFromZero),
            solicitud.PrecioPorLitro is { } p ? Math.Round(p, 4, MidpointRounding.AwayFromZero) : null,
            solicitud.ViajeId);
    }

    private static DateTimeOffset InicioDelDia(DateOnly fecha) =>
        new DateTimeOffset(fecha.ToDateTime(TimeOnly.MinValue), HoraElSalvador).ToUniversalTime();

    private static ApiException NoExiste() => ApiException.NoEncontrado("La carga de combustible no existe.");
}
