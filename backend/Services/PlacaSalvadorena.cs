using System.Text.RegularExpressions;

namespace EcoTrack.Services;

public static partial class PlacaSalvadorena
{
    private const string Particular = "P";
    private const int MaximoCaracteres = 6;

    public static IReadOnlyDictionary<string, string> Clases { get; } = new Dictionary<string, string>
    {
        ["P"] = "Particular",
        ["A"] = "Alquiler",
        ["C"] = "Camión",
        ["AB"] = "Autobús",
        ["MB"] = "Microbús",
        ["R"] = "Taller o reparación",
        ["N"] = "Nacional",
        ["O"] = "Oficial",
        ["D"] = "Personas con discapacidad",
        ["V"] = "Vendedor",
        ["PR"] = "Provisional",
        ["MI"] = "Misión internacional",
        ["M"] = "Motocicleta",
        ["RE"] = "Remolque",
        ["CD"] = "Cuerpo diplomático",
        ["CC"] = "Cuerpo consular",
        ["CR"] = "Cruz Roja",
    };

    public static string? Normalizar(string? placa)
    {
        if (string.IsNullOrWhiteSpace(placa))
        {
            return null;
        }

        var limpia = Separadores().Replace(placa, string.Empty).ToUpperInvariant();

        foreach (var clase in Clases.Keys.OrderByDescending(c => c.Length))
        {
            if (!limpia.StartsWith(clase, StringComparison.Ordinal))
            {
                continue;
            }

            var numero = limpia[clase.Length..];
            if (EsNumeroValido(clase, numero))
            {
                return $"{clase} {numero}";
            }
        }

        return null;
    }

    private static bool EsNumeroValido(string clase, string numero)
    {
        if (numero.Length is 0 or > MaximoCaracteres)
        {
            return false;
        }

        return clase == Particular
            ? numero.All(Uri.IsHexDigit)
            : numero.All(char.IsAsciiDigit);
    }

    [GeneratedRegex(@"[\s-]+")]
    private static partial Regex Separadores();
}
