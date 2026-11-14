using System.Globalization;
using System.Text.RegularExpressions;
using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Dtos;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrack.Services;

public sealed partial class UsuarioService(AppDbContext db, SesionVigente sesiones, SesionService refrescos)
{
    public const int LimiteMaximo = 100;
    private const int LargoMaximo = 80;

    private static readonly Dictionary<string, RolUsuario> Roles = new()
    {
        ["admin"] = RolUsuario.Admin,
        ["operador"] = RolUsuario.Operador,
        ["auditor"] = RolUsuario.Auditor,
    };

    public async Task<(IReadOnlyList<UsuarioRespuesta> Datos, MetaPaginacion Meta)> ListarAsync(
        UsuarioFiltro filtro, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var consulta = db.Usuarios.AsNoTracking();

        switch (filtro.Estado?.Trim().ToLowerInvariant())
        {
            case null or "" or "activo":
                consulta = consulta.Where(u => u.Activo);
                break;
            case "inactivo":
                consulta = consulta.Where(u => !u.Activo);
                break;
            case "todos":
                break;
            default:
                errores.Add(new ErrorDetalle("estado", "Usa activo, inactivo o todos."));
                break;
        }

        if (!string.IsNullOrWhiteSpace(filtro.Rol))
        {
            var rol = LeerRol(filtro.Rol, errores);
            consulta = consulta.Where(u => u.Rol == rol);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Q))
        {
            var patron = Busqueda.Patron(filtro.Q);
            consulta = consulta.Where(u =>
                Regex.IsMatch(u.Nombres + " " + u.Apellidos, patron, RegexOptions.IgnoreCase)
                || Regex.IsMatch(u.Correo, patron, RegexOptions.IgnoreCase));
        }

        if (filtro.Page < 1)
        {
            errores.Add(new ErrorDetalle("page", "La página empieza en 1."));
        }

        if (filtro.Limit is < 1 or > LimiteMaximo)
        {
            errores.Add(new ErrorDetalle("limit", string.Create(CultureInfo.InvariantCulture, $"El límite debe estar entre 1 y {LimiteMaximo}.")));
        }

        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var total = await consulta.CountAsync(cancelacion);
        var datos = await consulta
            .OrderBy(u => u.Apellidos).ThenBy(u => u.Nombres)
            .Skip((filtro.Page - 1) * filtro.Limit)
            .Take(filtro.Limit)
            .Select(u => new UsuarioRespuesta(u.UsuarioId, u.Correo, u.Nombres, u.Apellidos, u.Rol, u.Activo, u.CreadoEn, u.ActualizadoEn))
            .ToListAsync(cancelacion);

        var paginas = (int)Math.Ceiling(total / (double)filtro.Limit);
        return (datos, new MetaPaginacion(filtro.Page, filtro.Limit, total, paginas));
    }

    public async Task<UsuarioRespuesta> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        ARespuesta(await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.UsuarioId == id, cancelacion) ?? throw NoExiste());

    public async Task<UsuarioRespuesta> CrearAsync(UsuarioCrearSolicitud solicitud, Guid autorId, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        var correo = solicitud.Correo?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(correo) || correo.Length > 254 || !Correo().IsMatch(correo))
        {
            errores.Add(new ErrorDetalle("correo", "Escribe un correo válido, por ejemplo nombre@empresa.com."));
        }

        var nombres = Texto(solicitud.Nombres, "nombres", "Escribe los nombres.", errores);
        var apellidos = Texto(solicitud.Apellidos, "apellidos", "Escribe los apellidos.", errores);
        var rol = LeerRol(solicitud.Rol, errores);
        ContrasenaSegura.Validar(solicitud.Contrasena, "contrasena", errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        if (await db.Usuarios.AnyAsync(u => u.Correo == correo, cancelacion))
        {
            throw CorreoOcupado();
        }

        var ahora = DateTimeOffset.UtcNow;
        var usuario = new Usuario
        {
            UsuarioId = Guid.NewGuid(),
            Correo = correo!,
            HashContrasena = ContrasenaSegura.Hash(solicitud.Contrasena!),
            Nombres = nombres!,
            Apellidos = apellidos!,
            Rol = rol!.Value,
            Activo = true,
            CreadoEn = ahora,
            ActualizadoEn = ahora,
        };
        db.Usuarios.Add(usuario);
        await db.GuardarComoAsync(autorId, cancelacion);
        return ARespuesta(usuario);
    }

    public async Task<UsuarioRespuesta> ActualizarAsync(
        Guid id, UsuarioActualizarSolicitud solicitud, Guid autorId, CancellationToken cancelacion)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == id, cancelacion) ?? throw NoExiste();

        var errores = new List<ErrorDetalle>();
        var nombres = Texto(solicitud.Nombres, "nombres", "Escribe los nombres.", errores);
        var apellidos = Texto(solicitud.Apellidos, "apellidos", "Escribe los apellidos.", errores);
        var rol = LeerRol(solicitud.Rol, errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        if (usuario.Rol == RolUsuario.Admin && rol != RolUsuario.Admin && usuario.Activo)
        {
            await ExigirOtroAdministradorAsync(id, "No puedes quitar el rol de administrador al último administrador activo.", cancelacion);
        }

        usuario.Nombres = nombres!;
        usuario.Apellidos = apellidos!;
        usuario.Rol = rol!.Value;
        await db.GuardarComoAsync(autorId, cancelacion);
        sesiones.Olvidar(id);
        return await ObtenerAsync(id, cancelacion);
    }

    public async Task<UsuarioRespuesta> CambiarEstadoAsync(
        Guid id, UsuarioEstadoSolicitud solicitud, Guid autorId, CancellationToken cancelacion)
    {
        if (solicitud.Activo is null)
        {
            throw ApiException.Validacion([new ErrorDetalle("activo", "Indica si la cuenta queda activa o inactiva.")]);
        }

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == id, cancelacion) ?? throw NoExiste();
        if (usuario.Activo == solicitud.Activo)
        {
            return ARespuesta(usuario);
        }

        if (!solicitud.Activo.Value)
        {
            if (id == autorId)
            {
                throw ApiException.Conflicto("No puedes desactivar tu propia cuenta.");
            }

            if (usuario.Rol == RolUsuario.Admin)
            {
                await ExigirOtroAdministradorAsync(id, "No puedes desactivar al último administrador activo.", cancelacion);
            }
        }

        usuario.Activo = solicitud.Activo.Value;
        await db.GuardarComoAsync(autorId, cancelacion);
        sesiones.Olvidar(id);
        if (!usuario.Activo)
        {
            await refrescos.RevocarUsuarioAsync(id, cancelacion);
        }

        return await ObtenerAsync(id, cancelacion);
    }

    public async Task RestablecerContrasenaAsync(
        Guid id, ContrasenaRestablecerSolicitud solicitud, Guid autorId, CancellationToken cancelacion)
    {
        var errores = new List<ErrorDetalle>();
        ContrasenaSegura.Validar(solicitud.ContrasenaNueva, "contrasena_nueva", errores);
        if (errores.Count > 0)
        {
            throw ApiException.Validacion(errores);
        }

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == id, cancelacion) ?? throw NoExiste();
        usuario.HashContrasena = ContrasenaSegura.Hash(solicitud.ContrasenaNueva!);
        await db.GuardarComoAsync(autorId, cancelacion);
        sesiones.Olvidar(id);
        await refrescos.RevocarUsuarioAsync(id, cancelacion);
    }

    private async Task ExigirOtroAdministradorAsync(Guid id, string mensaje, CancellationToken cancelacion)
    {
        var otros = await db.Usuarios.CountAsync(u => u.Rol == RolUsuario.Admin && u.Activo && u.UsuarioId != id, cancelacion);
        if (otros == 0)
        {
            throw ApiException.Conflicto(mensaje);
        }
    }

    private static RolUsuario? LeerRol(string? valor, List<ErrorDetalle> errores)
    {
        if (valor is not null && Roles.TryGetValue(valor.Trim().ToLowerInvariant(), out var rol))
        {
            return rol;
        }

        errores.Add(new ErrorDetalle("rol", "Elige administrador, operador o auditor."));
        return null;
    }

    private static string? Texto(string? valor, string campo, string mensaje, List<ErrorDetalle> errores)
    {
        var limpio = string.IsNullOrWhiteSpace(valor) ? null : MultiplesEspacios().Replace(valor.Trim(), " ");
        if (limpio is null)
        {
            errores.Add(new ErrorDetalle(campo, mensaje));
        }
        else if (limpio.Length > LargoMaximo)
        {
            errores.Add(new ErrorDetalle(campo, "Usa como máximo 80 caracteres."));
        }

        return limpio;
    }

    private static UsuarioRespuesta ARespuesta(Usuario u) =>
        new(u.UsuarioId, u.Correo, u.Nombres, u.Apellidos, u.Rol, u.Activo, u.CreadoEn, u.ActualizadoEn);

    private static ApiException NoExiste() => ApiException.NoEncontrado("El usuario no existe.");

    public static ApiException CorreoOcupado() => ApiException.Conflicto("Ya existe un usuario con ese correo.",
        [new ErrorDetalle("correo", "Ya existe un usuario con este correo.")]);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Correo();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiplesEspacios();
}
