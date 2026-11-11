using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EcoTrack.Common;

public static class CodigosError
{
    public const string SolicitudInvalida = "SOLICITUD_INVALIDA";
    public const string Validacion = "VALIDACION";
    public const string NoAutenticado = "NO_AUTENTICADO";
    public const string SinPermiso = "SIN_PERMISO";
    public const string NoEncontrado = "NO_ENCONTRADO";
    public const string Conflicto = "CONFLICTO";
    public const string Interno = "ERROR_INTERNO";
}

public sealed record ErrorDetalle(string Campo, string Mensaje);

public sealed record ErrorCuerpo(string Code, string Message, IReadOnlyList<ErrorDetalle> Details, string TraceId);

public sealed record RespuestaError(ErrorCuerpo Error)
{
    public static RespuestaError Crear(
        string codigo, string mensaje, HttpContext contexto, IReadOnlyList<ErrorDetalle>? detalles = null) =>
        new(new ErrorCuerpo(codigo, mensaje, detalles ?? [], Activity.Current?.TraceId.ToString() ?? contexto.TraceIdentifier));
}

public sealed class ApiException(
    int estado, string codigo, string mensaje, IReadOnlyList<ErrorDetalle>? detalles = null) : Exception(mensaje)
{
    public int Estado { get; } = estado;
    public string Codigo { get; } = codigo;
    public IReadOnlyList<ErrorDetalle>? Detalles { get; } = detalles;

    public static ApiException NoEncontrado(string mensaje) =>
        new(StatusCodes.Status404NotFound, CodigosError.NoEncontrado, mensaje);

    public static ApiException Conflicto(string mensaje, IReadOnlyList<ErrorDetalle>? detalles = null) =>
        new(StatusCodes.Status409Conflict, CodigosError.Conflicto, mensaje, detalles);

    public static ApiException Validacion(IReadOnlyList<ErrorDetalle> detalles) =>
        new(StatusCodes.Status422UnprocessableEntity, CodigosError.Validacion, "Revisa los datos enviados.", detalles);

    public static ApiException NoAutenticado(string mensaje) =>
        new(StatusCodes.Status401Unauthorized, CodigosError.NoAutenticado, mensaje);
}

public sealed partial class ManejadorErrores(ILogger<ManejadorErrores> logger, IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> opciones)
    : IExceptionHandler
{
    private readonly JsonSerializerOptions serializador = opciones.Value.JsonSerializerOptions;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (estado, respuesta) = Traducir(exception, httpContext);
        if (estado >= StatusCodes.Status500InternalServerError)
        {
            RegistrarFallo(logger, httpContext.TraceIdentifier, exception);
        }

        httpContext.Response.StatusCode = estado;
        await httpContext.Response.WriteAsJsonAsync(respuesta, serializador, cancellationToken);
        return true;
    }

    public static async Task EscribirEstadoAsync(StatusCodeContext contexto)
    {
        var http = contexto.HttpContext;
        var (codigo, mensaje) = http.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => (CodigosError.NoAutenticado, "Debes iniciar sesión."),
            StatusCodes.Status403Forbidden => (CodigosError.SinPermiso, "No tienes permiso para esta operación."),
            StatusCodes.Status404NotFound => (CodigosError.NoEncontrado, "El recurso no existe."),
            _ => (CodigosError.SolicitudInvalida, "La solicitud no se pudo procesar."),
        };

        var serializador = http.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        await http.Response.WriteAsJsonAsync(RespuestaError.Crear(codigo, mensaje, http), serializador);
    }

    private static (int Estado, RespuestaError Respuesta) Traducir(Exception error, HttpContext contexto)
    {
        if (error is ApiException api)
        {
            return (api.Estado, RespuestaError.Crear(api.Codigo, api.Message, contexto, api.Detalles));
        }

        var postgres = error switch
        {
            DbUpdateException { InnerException: PostgresException interna } => interna,
            PostgresException directa => directa,
            _ => null,
        };

        if (postgres is not null)
        {
            var traducido = TraducirPostgres(postgres, contexto);
            if (traducido is not null)
            {
                return traducido.Value;
            }
        }

        return (StatusCodes.Status500InternalServerError,
            RespuestaError.Crear(CodigosError.Interno, "Ocurrió un error interno. Intenta de nuevo.", contexto));
    }

    private static (int Estado, RespuestaError Respuesta)? TraducirPostgres(PostgresException error, HttpContext contexto) =>
        error.ConstraintName switch
        {
            null when error.SqlState is PostgresErrorCodes.CheckViolation or PostgresErrorCodes.RestrictViolation
                or PostgresErrorCodes.IntegrityConstraintViolation => (StatusCodes.Status422UnprocessableEntity,
                RespuestaError.Crear(CodigosError.Validacion, error.MessageText, contexto)),
            "ux_auditoria_reporte_abierta" => (StatusCodes.Status409Conflict, RespuestaError.Crear(
                CodigosError.Conflicto, "Ya hay una auditoría abierta para este reporte.", contexto)),
            "chofer_numero_licencia_key" => (StatusCodes.Status409Conflict, RespuestaError.Crear(
                CodigosError.Conflicto, "Ya existe un chofer con ese número de licencia.", contexto,
                [new ErrorDetalle("numero_licencia", "Ya existe un chofer con este número de licencia.")])),
            "ux_usuario_correo_lower" => (StatusCodes.Status409Conflict, RespuestaError.Crear(
                CodigosError.Conflicto, "Ya existe un usuario con ese correo.", contexto,
                [new ErrorDetalle("correo", "Ya existe un usuario con este correo.")])),
            "ux_viaje_vehiculo_salida_activo" => (StatusCodes.Status409Conflict, RespuestaError.Crear(
                CodigosError.Conflicto, "Ya hay un viaje de este vehículo con la misma fecha y hora de salida.", contexto,
                [new ErrorDetalle("salida_en", "Este vehículo ya tiene un viaje que sale a esa hora.")])),
            "ux_vehiculo_placa_upper" => (StatusCodes.Status409Conflict, RespuestaError.Crear(
                CodigosError.Conflicto, "Ya existe un vehículo con esa placa.", contexto,
                [new ErrorDetalle("placa", "Ya existe un vehículo con esta placa.")])),
            "ex_factor_emision_sin_solape" => (StatusCodes.Status409Conflict, RespuestaError.Crear(
                CodigosError.Conflicto, "Ya hay un factor con esa vigencia para esta combinación.", contexto,
                [new ErrorDetalle("vigente_desde", "La fecha se solapa con otro factor de esta combinación.")])),
            _ => null,
        };

    [LoggerMessage(Level = LogLevel.Error, Message = "Error no controlado en la solicitud {TraceId}")]
    private static partial void RegistrarFallo(ILogger logger, string traceId, Exception error);
}
