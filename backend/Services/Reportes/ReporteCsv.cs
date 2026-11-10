using System.Globalization;
using System.Text;

namespace EcoTrack.Services.Reportes;

public sealed record FilaViajeReporte(
    Guid ViajeId,
    string Placa,
    string TipoVehiculo,
    string TipoCombustible,
    string? ChoferLicencia,
    string Origen,
    string Destino,
    DateTimeOffset SalidaEn,
    DateTimeOffset? LlegadaEn,
    decimal DistanciaKm,
    string Estado,
    decimal Co2Kg);

public sealed record FilaCsv(int Numero, IReadOnlyList<string> Campos);

public static class ReporteCsv
{
    public static readonly IReadOnlyList<string> Columnas =
    [
        "viaje_id", "vehiculo_placa", "tipo_vehiculo", "combustible", "chofer_licencia", "origen",
        "destino", "fecha_salida", "fecha_llegada", "distancia", "estado", "co2_kg_operativo",
    ];

    public const string FormatoFecha = "yyyy-MM-dd'T'HH:mm:sszzz";

    private static readonly TimeSpan HoraElSalvador = TimeSpan.FromHours(-6);
    private static readonly UTF8Encoding Utf8ConBom = new(encoderShouldEmitUTF8Identifier: true);

    public static byte[] Escribir(IEnumerable<FilaViajeReporte> filas)
    {
        var texto = new StringBuilder();
        texto.Append(string.Join(',', Columnas)).Append("\r\n");

        foreach (var fila in filas)
        {
            string[] campos =
            [
                fila.ViajeId.ToString(),
                fila.Placa,
                fila.TipoVehiculo,
                fila.TipoCombustible,
                fila.ChoferLicencia ?? string.Empty,
                fila.Origen,
                fila.Destino,
                Fecha(fila.SalidaEn),
                fila.LlegadaEn is { } llegada ? Fecha(llegada) : string.Empty,
                fila.DistanciaKm.ToString("0.00", CultureInfo.InvariantCulture),
                fila.Estado,
                fila.Co2Kg.ToString("0.0000", CultureInfo.InvariantCulture),
            ];
            texto.Append(string.Join(',', campos.Select(Celda))).Append("\r\n");
        }

        return Utf8ConBom.GetPreamble().Concat(Utf8ConBom.GetBytes(texto.ToString())).ToArray();
    }

    public static (IReadOnlyList<string> Encabezado, IReadOnlyList<FilaCsv> Filas) Leer(byte[] contenido)
    {
        var texto = new UTF8Encoding(false).GetString(contenido).TrimStart('﻿');
        var registros = Separar(texto);
        if (registros.Count == 0)
        {
            return ([], []);
        }

        var filas = registros.Skip(1)
            .Select((campos, indice) => new FilaCsv(indice + 2, campos.Select(Restaurar).ToList()))
            .Where(f => !(f.Campos.Count == 1 && f.Campos[0].Length == 0))
            .ToList();
        return (registros[0], filas);
    }

    public static bool LeerFecha(string valor, out DateTimeOffset fecha) =>
        DateTimeOffset.TryParseExact(valor, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);

    public static bool LeerDecimal(string valor, out decimal numero) =>
        decimal.TryParse(valor, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out numero);

    private static string Fecha(DateTimeOffset fecha) =>
        fecha.ToOffset(HoraElSalvador).ToString(FormatoFecha, CultureInfo.InvariantCulture);

    private static string Celda(string valor)
    {
        if (valor.Length > 0 && "=+-@\t\r".Contains(valor[0], StringComparison.Ordinal))
        {
            valor = "'" + valor;
        }

        return valor.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + valor.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : valor;
    }

    private static string Restaurar(string valor) =>
        valor.Length > 1 && valor[0] == '\'' && "=+-@\t\r".Contains(valor[1], StringComparison.Ordinal)
            ? valor[1..]
            : valor;

    private static List<List<string>> Separar(string texto)
    {
        var registros = new List<List<string>>();
        var actual = new List<string>();
        var campo = new StringBuilder();
        var entreComillas = false;

        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (entreComillas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    campo.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    entreComillas = false;
                }
                else
                {
                    campo.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    entreComillas = true;
                    break;
                case ',':
                    actual.Add(campo.ToString());
                    campo.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    actual.Add(campo.ToString());
                    campo.Clear();
                    registros.Add(actual);
                    actual = [];
                    break;
                default:
                    campo.Append(c);
                    break;
            }
        }

        if (campo.Length > 0 || actual.Count > 0)
        {
            actual.Add(campo.ToString());
            registros.Add(actual);
        }

        return registros;
    }
}
