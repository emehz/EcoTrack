using System.Collections.Concurrent;
using System.Globalization;
using EcoTrack.Common;

namespace EcoTrack.Services;

public sealed class IntentosLogin(TimeProvider reloj)
{
    public const int IntentosMaximos = 5;
    public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Registro> registros = new();

    public void ExigirPermitido(string clave)
    {
        if (!registros.TryGetValue(clave, out var registro))
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        if (ahora - registro.Inicio >= Ventana)
        {
            registros.TryRemove(clave, out _);
            return;
        }

        if (registro.Fallos >= IntentosMaximos)
        {
            var minutos = (int)Math.Ceiling((registro.Inicio + Ventana - ahora).TotalMinutes);
            throw new ApiException(StatusCodes.Status429TooManyRequests, "DEMASIADOS_INTENTOS",
                string.Create(CultureInfo.InvariantCulture,
                    $"Demasiados intentos fallidos. Espera {minutos} {(minutos == 1 ? "minuto" : "minutos")} e inténtalo de nuevo."));
        }
    }

    public void RegistrarFallo(string clave)
    {
        var ahora = reloj.GetUtcNow();
        registros.AddOrUpdate(
            clave,
            _ => new Registro(ahora, 1),
            (_, actual) => ahora - actual.Inicio >= Ventana ? new Registro(ahora, 1) : actual with { Fallos = actual.Fallos + 1 });
    }

    public void Limpiar(string clave) => registros.TryRemove(clave, out _);

    private sealed record Registro(DateTimeOffset Inicio, int Fallos);
}
