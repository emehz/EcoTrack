using System.Globalization;

namespace EcoTrack.Services.Auditorias;

public sealed record ParametrosAuditoria(decimal ToleranciaCo2Kg, decimal UmbralDistanciaKm)
{
    public static ParametrosAuditoria Desde(IConfiguration configuracion) => new(
        Leer(configuracion["AUDITORIA_TOLERANCIA_CO2_KG"], 0.0001m),
        Leer(configuracion["AUDITORIA_UMBRAL_DISTANCIA_KM"], 1000m));

    private static decimal Leer(string? valor, decimal porDefecto) =>
        decimal.TryParse(valor, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numero) && numero > 0
            ? numero
            : porDefecto;
}
