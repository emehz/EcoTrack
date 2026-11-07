using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services;

public sealed class VehiculoService(AppDbContext db, FactorEmisionService factores)
{
    public const int LimiteMaximo = 100;
    private const string EstadoTodos = "todos";

    public async Task<(IReadOnlyList<VehiculoRespuesta> Datos, MetaPaginacion Meta)> ListarAsync(
        VehiculoFiltro filtro, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var consulta = AplicarFiltros(db.Vehiculos.AsNoTracking(), filtro, errores);
        var estado = LeerFiltroEstado(filtro.Estado, errores);
        ValidarPaginacion(filtro, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var conteos = await ContarPorEstadoAsync(consulta, cancelacion);
        if (estado is not null)
        {
            consulta = consulta.Where(v => v.Estado == estado);
        }

        var total = estado switch
        {
            EstadoVehiculo.Activo => conteos.Activo,
            EstadoVehiculo.Mantenimiento => conteos.Mantenimiento,
            EstadoVehiculo.Inactivo => conteos.Inactivo,
            _ => conteos.Todos,
        };

        var datos = await Proyectar(Ordenar(consulta, filtro))
            .Skip((filtro.Page - 1) * filtro.Limit)
            .Take(filtro.Limit)
            .ToListAsync(cancelacion);

        var paginas = (int)Math.Ceiling(total / (double)filtro.Limit);
        return (datos, new MetaPaginacion(filtro.Page, filtro.Limit, total, paginas, conteos));
    }

    public async Task<VehiculoDetalleRespuesta> ObtenerAsync(Guid id, CancellationToken cancelacion)
    {
        var vehiculo = await Proyectar(db.Vehiculos.AsNoTracking().Where(v => v.VehiculoId == id))
            .FirstOrDefaultAsync(cancelacion)
            ?? throw NoExiste();

        var viajes = await db.Viajes.AsNoTracking()
            .Where(v => v.VehiculoId == id && v.Estado == EstadoViaje.Completado)
            .GroupBy(_ => 1)
            .Select(g => new { Cantidad = g.Count(), Km = g.Sum(v => v.DistanciaKm), Co2 = g.Sum(v => v.Co2Kg) })
            .FirstOrDefaultAsync(cancelacion);

        var carga = await db.CargasCombustible.AsNoTracking()
            .Where(c => c.VehiculoId == id)
            .OrderByDescending(c => c.CargadoEn)
            .Select(c => new { c.CargadoEn, c.Litros })
            .FirstOrDefaultAsync(cancelacion);

        return new VehiculoDetalleRespuesta(
            vehiculo, viajes?.Cantidad ?? 0, viajes?.Km ?? 0, viajes?.Co2 ?? 0, carga?.CargadoEn, carga?.Litros);
    }

    public async Task<VehiculoRespuesta> CrearAsync(VehiculoCrearSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var placa = PlacaSalvadorena.Normalizar(solicitud.Placa);
        if (placa is null)
        {
            errores.Add(new ErrorDetalle("placa", "Escribe una placa válida de El Salvador, por ejemplo C 48213 o P 123456."));
        }

        var tipo = Catalogos.LeerTipo(solicitud.TipoVehiculo, "tipo_vehiculo", errores);
        var combustible = Catalogos.LeerCombustible(solicitud.TipoCombustible, "tipo_combustible", errores);
        var estado = solicitud.Estado is null
            ? EstadoVehiculo.Activo
            : LeerEstadoOperativo(solicitud.Estado, errores);

        if (placa is not null && await db.Vehiculos.AnyAsync(v => v.Placa == placa, cancelacion))
        {
            throw ApiException.Conflicto("Ya existe un vehículo con esa placa.", [new ErrorDetalle("placa", "Ya existe un vehículo con esta placa.")]);
        }

        if (tipo is not null && combustible is not null)
        {
            await ExigirFactorVigenteAsync(tipo.Value, combustible.Value, errores, cancelacion);
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var vehiculo = new Vehiculo
        {
            Placa = placa!,
            TipoVehiculo = tipo!.Value,
            TipoCombustible = combustible!.Value,
            Estado = estado ?? EstadoVehiculo.Activo,
        };
        db.Vehiculos.Add(vehiculo);
        await db.GuardarComoAsync(usuarioId, cancelacion);

        return await LeerRespuestaAsync(vehiculo.VehiculoId, cancelacion);
    }

    public async Task<VehiculoRespuesta> ActualizarAsync(
        Guid id, VehiculoActualizarSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var vehiculo = await db.Vehiculos.FirstOrDefaultAsync(v => v.VehiculoId == id, cancelacion) ?? throw NoExiste();
        var errores = new List<ErrorDetalle>();

        if (solicitud.TipoVehiculo is null && solicitud.TipoCombustible is null && solicitud.Estado is null)
        {
            errores.Add(new ErrorDetalle("cuerpo", "Indica al menos un campo para actualizar."));
        }

        var tipo = solicitud.TipoVehiculo is null
            ? vehiculo.TipoVehiculo
            : Catalogos.LeerTipo(solicitud.TipoVehiculo, "tipo_vehiculo", errores);
        var combustible = solicitud.TipoCombustible is null
            ? vehiculo.TipoCombustible
            : Catalogos.LeerCombustible(solicitud.TipoCombustible, "tipo_combustible", errores);
        var estado = solicitud.Estado is null
            ? vehiculo.Estado
            : LeerEstadoOperativo(solicitud.Estado, errores);

        if (tipo is not null && combustible is not null
            && (tipo != vehiculo.TipoVehiculo || combustible != vehiculo.TipoCombustible))
        {
            await ExigirFactorVigenteAsync(tipo.Value, combustible.Value, errores, cancelacion);
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        vehiculo.TipoVehiculo = tipo!.Value;
        vehiculo.TipoCombustible = combustible!.Value;
        vehiculo.Estado = estado!.Value;
        await db.GuardarComoAsync(usuarioId, cancelacion);

        return await LeerRespuestaAsync(id, cancelacion);
    }

    public async Task<VehiculoRespuesta> DarDeBajaAsync(
        Guid id, VehiculoEstadoSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        if (Catalogos.LeerEstado(solicitud.Estado, "estado", errores) != EstadoVehiculo.Inactivo)
        {
            throw ApiException.Validacion([new ErrorDetalle("estado", "Este endpoint solo da de baja: envía estado inactivo.")]);
        }

        var vehiculo = await db.Vehiculos.FirstOrDefaultAsync(v => v.VehiculoId == id, cancelacion) ?? throw NoExiste();
        if (vehiculo.Estado != EstadoVehiculo.Inactivo)
        {
            vehiculo.Estado = EstadoVehiculo.Inactivo;
            await db.GuardarComoAsync(usuarioId, cancelacion);
        }

        return await LeerRespuestaAsync(id, cancelacion);
    }

    private async Task<VehiculoRespuesta> LeerRespuestaAsync(Guid id, CancellationToken cancelacion) =>
        await Proyectar(db.Vehiculos.AsNoTracking().Where(v => v.VehiculoId == id)).FirstAsync(cancelacion);

    private async Task ExigirFactorVigenteAsync(
        TipoVehiculo tipo, TipoCombustible combustible, List<ErrorDetalle> errores, CancellationToken cancelacion)
    {
        var existe = await factores.BuscarVigente(tipo, combustible, FechasElSalvador.Hoy()).AnyAsync(cancelacion);
        if (!existe)
        {
            errores.Add(new ErrorDetalle("tipo_combustible", "No hay factor de emisión vigente para esa combinación de tipo y combustible."));
        }
    }

    private static EstadoVehiculo? LeerEstadoOperativo(string valor, List<ErrorDetalle> errores)
    {
        var estado = Catalogos.LeerEstado(valor, "estado", errores);
        if (estado == EstadoVehiculo.Inactivo)
        {
            errores.Add(new ErrorDetalle("estado", "Para dejarlo inactivo usa la baja del vehículo."));
        }

        return estado;
    }

    private static EstadoVehiculo? LeerFiltroEstado(string? estado, List<ErrorDetalle> errores)
    {
        if (string.IsNullOrWhiteSpace(estado))
        {
            return EstadoVehiculo.Activo;
        }

        return string.Equals(estado.Trim(), EstadoTodos, StringComparison.OrdinalIgnoreCase)
            ? null
            : Catalogos.LeerEstado(estado, "estado", errores);
    }

    private static void ValidarPaginacion(VehiculoFiltro filtro, List<ErrorDetalle> errores)
    {
        if (filtro.Page < 1)
        {
            errores.Add(new ErrorDetalle("page", "La página empieza en 1."));
        }

        if (filtro.Limit is < 1 or > LimiteMaximo)
        {
            errores.Add(new ErrorDetalle("limit", $"El límite debe estar entre 1 y {LimiteMaximo}."));
        }

        if (filtro.Order is not (null or "asc" or "desc"))
        {
            errores.Add(new ErrorDetalle("order", "Usa asc o desc."));
        }

        if (filtro.Sort is not (null or "placa" or "tipo_vehiculo" or "tipo_combustible" or "estado" or "creado_en"))
        {
            errores.Add(new ErrorDetalle("sort", "Ordena por placa, tipo_vehiculo, tipo_combustible, estado o creado_en."));
        }
    }

    private static IQueryable<Vehiculo> AplicarFiltros(IQueryable<Vehiculo> consulta, VehiculoFiltro filtro, List<ErrorDetalle> errores)
    {
        if (!string.IsNullOrWhiteSpace(filtro.Q))
        {
            var patron = $"%{filtro.Q.Trim().Replace("-", " ", StringComparison.Ordinal)}%";
            consulta = consulta.Where(v => EF.Functions.ILike(v.Placa, patron));
        }

        if (!string.IsNullOrWhiteSpace(filtro.TipoVehiculo))
        {
            var tipo = Catalogos.LeerTipo(filtro.TipoVehiculo, "tipo_vehiculo", errores);
            consulta = consulta.Where(v => v.TipoVehiculo == tipo);
        }

        if (!string.IsNullOrWhiteSpace(filtro.TipoCombustible))
        {
            var combustible = Catalogos.LeerCombustible(filtro.TipoCombustible, "tipo_combustible", errores);
            consulta = consulta.Where(v => v.TipoCombustible == combustible);
        }

        return consulta;
    }

    private static IQueryable<Vehiculo> Ordenar(IQueryable<Vehiculo> consulta, VehiculoFiltro filtro)
    {
        var descendente = filtro.Order == "desc";
        return filtro.Sort switch
        {
            "tipo_vehiculo" => descendente ? consulta.OrderByDescending(v => v.TipoVehiculo).ThenBy(v => v.Placa) : consulta.OrderBy(v => v.TipoVehiculo).ThenBy(v => v.Placa),
            "tipo_combustible" => descendente ? consulta.OrderByDescending(v => v.TipoCombustible).ThenBy(v => v.Placa) : consulta.OrderBy(v => v.TipoCombustible).ThenBy(v => v.Placa),
            "estado" => descendente ? consulta.OrderByDescending(v => v.Estado).ThenBy(v => v.Placa) : consulta.OrderBy(v => v.Estado).ThenBy(v => v.Placa),
            "creado_en" => descendente ? consulta.OrderByDescending(v => v.CreadoEn) : consulta.OrderBy(v => v.CreadoEn),
            _ => descendente ? consulta.OrderByDescending(v => v.Placa) : consulta.OrderBy(v => v.Placa),
        };
    }

    private static async Task<ConteosPorEstado> ContarPorEstadoAsync(IQueryable<Vehiculo> consulta, CancellationToken cancelacion)
    {
        var grupos = await consulta
            .GroupBy(v => v.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(cancelacion);

        int Contar(EstadoVehiculo estado) => grupos.Find(g => g.Estado == estado)?.Cantidad ?? 0;

        return new ConteosPorEstado(
            Contar(EstadoVehiculo.Activo),
            Contar(EstadoVehiculo.Mantenimiento),
            Contar(EstadoVehiculo.Inactivo),
            grupos.Sum(g => g.Cantidad));
    }

    private IQueryable<VehiculoRespuesta> Proyectar(IQueryable<Vehiculo> consulta)
    {
        var hoy = FechasElSalvador.Hoy();
        return consulta.Select(v => new VehiculoRespuesta(
            v.VehiculoId,
            v.Placa,
            v.TipoVehiculo,
            v.TipoCombustible,
            v.Estado,
            db.FactoresEmision
                .Where(f => f.TipoVehiculo == v.TipoVehiculo && f.TipoCombustible == v.TipoCombustible
                    && f.VigenteDesde <= hoy && (f.VigenteHasta == null || f.VigenteHasta > hoy))
                .OrderByDescending(f => f.VigenteDesde)
                .Select(f => (decimal?)f.FactorCo2Km)
                .FirstOrDefault(),
            db.Viajes.Where(viaje => viaje.VehiculoId == v.VehiculoId).Max(viaje => (DateTimeOffset?)viaje.SalidaEn),
            v.CreadoEn,
            v.ActualizadoEn));
    }

    private static ApiException NoExiste() => ApiException.NoEncontrado("El vehículo no existe.");
}
