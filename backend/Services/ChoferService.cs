using System.Globalization;
using System.Text.RegularExpressions;
using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services;

public sealed partial class ChoferService(AppDbContext db)
{
    public const int LimiteMaximo = 100;
    private const int LargoMaximoNombre = 80;
    private const int LargoMaximoLicencia = 30;

    private static readonly Dictionary<string, EstadoChofer> Estados = new()
    {
        ["activo"] = EstadoChofer.Activo,
        ["inactivo"] = EstadoChofer.Inactivo,
        ["suspendido"] = EstadoChofer.Suspendido,
    };

    public async Task<(IReadOnlyList<ChoferRespuesta> Datos, MetaChoferes Meta)> ListarAsync(
        ChoferFiltro filtro, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        EstadoChofer? estado = EstadoChofer.Activo;
        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            var valor = filtro.Estado.Trim().ToLowerInvariant();
            estado = valor == "todos" ? null : LeerEstado(valor, errores);
        }

        ValidarListado(filtro, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var consulta = db.Choferes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtro.Q))
        {
            var patron = Busqueda.Patron(filtro.Q);
            consulta = consulta.Where(c =>
                Regex.IsMatch(c.Nombres + " " + c.Apellidos, patron, RegexOptions.IgnoreCase)
                || Regex.IsMatch(c.NumeroLicencia, patron, RegexOptions.IgnoreCase));
        }

        var grupos = await consulta.GroupBy(c => c.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(cancelacion);
        int Contar(EstadoChofer e) => grupos.Find(g => g.Estado == e)?.Cantidad ?? 0;
        var conteos = new ConteosChofer(
            Contar(EstadoChofer.Activo), Contar(EstadoChofer.Inactivo), Contar(EstadoChofer.Suspendido), grupos.Sum(g => g.Cantidad));

        if (estado is not null)
        {
            consulta = consulta.Where(c => c.Estado == estado);
        }

        var total = estado switch
        {
            EstadoChofer.Activo => conteos.Activo,
            EstadoChofer.Inactivo => conteos.Inactivo,
            EstadoChofer.Suspendido => conteos.Suspendido,
            _ => conteos.Todos,
        };

        var datos = await Ordenar(consulta, filtro)
            .Skip((filtro.Page - 1) * filtro.Limit)
            .Take(filtro.Limit)
            .Select(c => new ChoferRespuesta(c.ChoferId, c.Nombres, c.Apellidos, c.NumeroLicencia, c.Telefono, c.Estado))
            .ToListAsync(cancelacion);

        var paginas = (int)Math.Ceiling(total / (double)filtro.Limit);
        return (datos, new MetaChoferes(filtro.Page, filtro.Limit, total, paginas, conteos));
    }

    public async Task<ChoferDetalleRespuesta> ObtenerAsync(Guid id, CancellationToken cancelacion)
    {
        var chofer = await db.Choferes.AsNoTracking().FirstOrDefaultAsync(c => c.ChoferId == id, cancelacion)
            ?? throw NoExiste();

        var resumen = await db.Database.SqlQuery<ResumenViajesChofer>($"""
            SELECT count(*)::int AS "Cantidad",
                   coalesce(sum(distancia_km), 0) AS "Km",
                   max(salida_en) AS "Ultimo"
              FROM viaje
             WHERE chofer_id = {id} AND estado = 'completado'
            """).FirstAsync(cancelacion);

        return new ChoferDetalleRespuesta(ARespuesta(chofer), resumen.Cantidad, resumen.Km, resumen.Ultimo);
    }

    public async Task<ChoferRespuesta> CrearAsync(ChoferGuardarSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var datos = Validar(solicitud, errores);
        if (errores.Count == 0)
        {
            await ExigirLicenciaLibreAsync(datos.Licencia, null, cancelacion);
        }
        else
        {
            throw ApiException.Validacion(errores);
        }

        var chofer = new Chofer
        {
            ChoferId = Guid.NewGuid(),
            Nombres = datos.Nombres,
            Apellidos = datos.Apellidos,
            NumeroLicencia = datos.Licencia,
            Telefono = datos.Telefono,
            Estado = EstadoChofer.Activo,
        };
        db.Choferes.Add(chofer);
        await db.GuardarComoAsync(usuarioId, cancelacion);
        return ARespuesta(chofer);
    }

    public async Task<ChoferRespuesta> ActualizarAsync(
        Guid id, ChoferGuardarSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var chofer = await db.Choferes.FirstOrDefaultAsync(c => c.ChoferId == id, cancelacion) ?? throw NoExiste();

        var errores = new List<ErrorDetalle>();
        var datos = Validar(solicitud, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        await ExigirLicenciaLibreAsync(datos.Licencia, id, cancelacion);
        chofer.Nombres = datos.Nombres;
        chofer.Apellidos = datos.Apellidos;
        chofer.NumeroLicencia = datos.Licencia;
        chofer.Telefono = datos.Telefono;
        await db.GuardarComoAsync(usuarioId, cancelacion);
        return ARespuesta(chofer);
    }

    public async Task<ChoferRespuesta> CambiarEstadoAsync(
        Guid id, ChoferEstadoSolicitud solicitud, Guid usuarioId, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var estado = LeerEstado(solicitud.Estado, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var chofer = await db.Choferes.FirstOrDefaultAsync(c => c.ChoferId == id, cancelacion) ?? throw NoExiste();
        if (chofer.Estado != estado)
        {
            chofer.Estado = estado!.Value;
            await db.GuardarComoAsync(usuarioId, cancelacion);
        }

        return ARespuesta(chofer);
    }

    private async Task ExigirLicenciaLibreAsync(string licencia, Guid? excepto, CancellationToken cancelacion)
    {
        var ocupada = await db.Choferes.AnyAsync(
            c => c.NumeroLicencia == licencia && c.ChoferId != excepto, cancelacion);
        if (ocupada)
        {
            throw ApiException.Conflicto("Ya existe un chofer con ese número de licencia.",
                [new ErrorDetalle("numero_licencia", "Ya existe un chofer con este número de licencia.")]);
        }
    }

    private static (string Nombres, string Apellidos, string Licencia, string? Telefono) Validar(
        ChoferGuardarSolicitud solicitud, List<ErrorDetalle> errores)
    {
        var nombres = Texto(solicitud.Nombres);
        var apellidos = Texto(solicitud.Apellidos);
        var licencia = Texto(solicitud.NumeroLicencia)?.ToUpperInvariant();
        var telefono = Texto(solicitud.Telefono);

        ValidarNombre(nombres, "nombres", "Escribe los nombres.", errores);
        ValidarNombre(apellidos, "apellidos", "Escribe los apellidos.", errores);

        if (licencia is null)
        {
            errores.Add(new ErrorDetalle("numero_licencia", "Escribe el número de licencia."));
        }
        else if (licencia.Length > LargoMaximoLicencia || !Licencia().IsMatch(licencia))
        {
            errores.Add(new ErrorDetalle("numero_licencia", "Usa solo letras, números y guiones, hasta 30 caracteres."));
        }

        if (telefono is not null)
        {
            var digitos = Digitos().Replace(telefono, string.Empty);
            if (Telefono().IsMatch(digitos))
            {
                telefono = string.Create(CultureInfo.InvariantCulture, $"{digitos[..4]}-{digitos[4..]}");
            }
            else
            {
                errores.Add(new ErrorDetalle("telefono", "Escribe un teléfono de El Salvador de 8 dígitos, por ejemplo 7777-1111."));
            }
        }

        return (nombres ?? string.Empty, apellidos ?? string.Empty, licencia ?? string.Empty, telefono);
    }

    private static void ValidarNombre(string? valor, string campo, string mensaje, List<ErrorDetalle> errores)
    {
        if (valor is null)
        {
            errores.Add(new ErrorDetalle(campo, mensaje));
        }
        else if (valor.Length > LargoMaximoNombre)
        {
            errores.Add(new ErrorDetalle(campo, "Usa como máximo 80 caracteres."));
        }
    }

    private static EstadoChofer? LeerEstado(string? valor, List<ErrorDetalle> errores)
    {
        if (valor is not null && Estados.TryGetValue(valor.Trim().ToLowerInvariant(), out var estado))
        {
            return estado;
        }

        errores.Add(new ErrorDetalle("estado", "Elige activo, inactivo o suspendido."));
        return null;
    }

    private static void ValidarListado(ChoferFiltro filtro, List<ErrorDetalle> errores)
    {
        if (filtro.Page < 1)
        {
            errores.Add(new ErrorDetalle("page", "La página empieza en 1."));
        }

        if (filtro.Limit is < 1 or > LimiteMaximo)
        {
            errores.Add(new ErrorDetalle("limit", string.Create(CultureInfo.InvariantCulture, $"El límite debe estar entre 1 y {LimiteMaximo}.")));
        }

        if (filtro.Order is not (null or "asc" or "desc"))
        {
            errores.Add(new ErrorDetalle("order", "Usa asc o desc."));
        }

        if (filtro.Sort is not (null or "nombre" or "numero_licencia" or "estado"))
        {
            errores.Add(new ErrorDetalle("sort", "Ordena por nombre, numero_licencia o estado."));
        }
    }

    private static IQueryable<Chofer> Ordenar(IQueryable<Chofer> consulta, ChoferFiltro filtro)
    {
        var descendente = filtro.Order == "desc";
        return filtro.Sort switch
        {
            "numero_licencia" => descendente ? consulta.OrderByDescending(c => c.NumeroLicencia) : consulta.OrderBy(c => c.NumeroLicencia),
            "estado" => descendente
                ? consulta.OrderByDescending(c => c.Estado).ThenBy(c => c.Apellidos)
                : consulta.OrderBy(c => c.Estado).ThenBy(c => c.Apellidos),
            _ => descendente
                ? consulta.OrderByDescending(c => c.Apellidos).ThenByDescending(c => c.Nombres)
                : consulta.OrderBy(c => c.Apellidos).ThenBy(c => c.Nombres),
        };
    }

    private static ChoferRespuesta ARespuesta(Chofer c) =>
        new(c.ChoferId, c.Nombres, c.Apellidos, c.NumeroLicencia, c.Telefono, c.Estado);

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : MultiplesEspacios().Replace(valor.Trim(), " ");

    private static ApiException NoExiste() => ApiException.NoEncontrado("El chofer no existe.");

    [GeneratedRegex("^[A-Z0-9-]+$")]
    private static partial Regex Licencia();

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex Digitos();

    [GeneratedRegex("^[2-7][0-9]{7}$")]
    private static partial Regex Telefono();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiplesEspacios();
}
