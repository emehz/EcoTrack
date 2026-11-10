using System.Globalization;
using System.Text.RegularExpressions;

namespace EcoTrack.Services.Almacenamiento;

public static partial class ClavesAlmacenamiento
{
    public const string PrefijoReportes = "reportes/";
    public const string PrefijoResultados = "resultados/";

    public static string Reporte(Guid reporteId, int numeroVersion) =>
        string.Create(CultureInfo.InvariantCulture, $"{PrefijoReportes}{reporteId}/v{numeroVersion}.csv");

    public static string ResultadoHuella(Guid reporteId, int numeroVersion) =>
        string.Create(CultureInfo.InvariantCulture, $"{PrefijoResultados}huella/{reporteId}/v{numeroVersion}.json");

    public static void Validar(string clave)
    {
        var permitida = !string.IsNullOrWhiteSpace(clave)
            && (clave.StartsWith(PrefijoReportes, StringComparison.Ordinal)
                || clave.StartsWith(PrefijoResultados, StringComparison.Ordinal))
            && !clave.Contains("..", StringComparison.Ordinal)
            && Permitida().IsMatch(clave);

        if (!permitida)
        {
            throw new ArgumentException($"La clave de almacenamiento no es válida: {clave}", nameof(clave));
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9/._-]*$")]
    private static partial Regex Permitida();
}
