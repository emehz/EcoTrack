using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EcoTrack.Common;

namespace EcoTrack.Services;

public static partial class DatosPersona
{
    public const int LargoMinimoNombre = 2;
    public const int LargoMaximoNombre = 80;

    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-SV");
    private static readonly HashSet<string> Particulas = ["de", "del", "la", "las", "los", "y"];

    public static string? Nombre(string? valor, string campo, string mensajeVacio, List<ErrorDetalle> errores)
    {
        var limpio = string.IsNullOrWhiteSpace(valor) ? null : Espacios().Replace(valor.Trim(), " ");
        if (limpio is null)
        {
            errores.Add(new ErrorDetalle(campo, mensajeVacio));
            return null;
        }

        if (!SoloLetras().IsMatch(limpio))
        {
            errores.Add(new ErrorDetalle(campo, "Usa solo letras; se permiten espacios, guiones y apóstrofos entre palabras."));
            return null;
        }

        if (limpio.Length < LargoMinimoNombre)
        {
            errores.Add(new ErrorDetalle(campo, "Escribe al menos 2 letras."));
            return null;
        }

        if (limpio.Length > LargoMaximoNombre)
        {
            errores.Add(new ErrorDetalle(campo, "Usa como máximo 80 caracteres."));
            return null;
        }

        return Capitalizar(limpio);
    }

    public static string? Licencia(string? valor, List<ErrorDetalle> errores)
    {
        const string campo = "numero_licencia";
        if (string.IsNullOrWhiteSpace(valor))
        {
            errores.Add(new ErrorDetalle(campo, "Escribe el número de licencia."));
            return null;
        }

        var limpio = valor.Trim();
        var digitos = Separadores().Replace(limpio, string.Empty);
        if (!SoloDigitos().IsMatch(digitos))
        {
            errores.Add(new ErrorDetalle(campo, "Usa solo números y guiones, con el formato del DUI (00000000-0) o del NIT (0000-000000-000-0)."));
            return null;
        }

        return digitos.Length switch
        {
            9 => string.Create(CultureInfo.InvariantCulture, $"{digitos[..8]}-{digitos[8..]}"),
            14 => string.Create(CultureInfo.InvariantCulture, $"{digitos[..4]}-{digitos[4..10]}-{digitos[10..13]}-{digitos[13..]}"),
            _ => Invalida(errores, campo),
        };
    }

    private static string? Invalida(List<ErrorDetalle> errores, string campo)
    {
        errores.Add(new ErrorDetalle(campo, "La licencia lleva 9 dígitos (DUI, 00000000-0) o 14 dígitos (NIT, 0000-000000-000-0)."));
        return null;
    }

    private static string Capitalizar(string texto)
    {
        var resultado = new StringBuilder(texto.Length);
        var palabras = texto.ToLower(Espanol).Split(' ');
        for (var i = 0; i < palabras.Length; i++)
        {
            if (i > 0)
            {
                resultado.Append(' ');
            }

            var palabra = palabras[i];
            resultado.Append(i > 0 && Particulas.Contains(palabra) ? palabra : MayusculaInicial(palabra));
        }

        return resultado.ToString();
    }

    private static string MayusculaInicial(string palabra)
    {
        var letras = palabra.ToCharArray();
        var inicioDeParte = true;
        for (var i = 0; i < letras.Length; i++)
        {
            if (inicioDeParte && char.IsLetter(letras[i]))
            {
                letras[i] = char.ToUpper(letras[i], Espanol);
            }

            inicioDeParte = letras[i] is '-' or '\'' or '’';
        }

        return new string(letras);
    }

    [GeneratedRegex(@"^\p{L}+(?:[ '’-]\p{L}+)*$")]
    private static partial Regex SoloLetras();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex Separadores();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex SoloDigitos();
}
