using System.Text;
using System.Text.RegularExpressions;

namespace EcoTrack.Common;

public static class Busqueda
{
    private static readonly Dictionary<char, string> Variantes = new()
    {
        ['a'] = "[aá]",
        ['á'] = "[aá]",
        ['e'] = "[eé]",
        ['é'] = "[eé]",
        ['i'] = "[ií]",
        ['í'] = "[ií]",
        ['o'] = "[oó]",
        ['ó'] = "[oó]",
        ['u'] = "[uúü]",
        ['ú'] = "[uúü]",
        ['ü'] = "[uúü]",
        ['n'] = "[nñ]",
        ['ñ'] = "[nñ]",
    };

    public static string Patron(string texto)
    {
        var patron = new StringBuilder();
        var espacioPrevio = false;
        foreach (var caracter in texto.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(caracter))
            {
                if (!espacioPrevio)
                {
                    patron.Append(@"\s+");
                }

                espacioPrevio = true;
                continue;
            }

            espacioPrevio = false;
            patron.Append(Variantes.TryGetValue(caracter, out var variante) ? variante : Regex.Escape(caracter.ToString()));
        }

        return patron.ToString();
    }
}
