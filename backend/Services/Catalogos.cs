using EcoTrack.Common;
using EcoTrack.Dtos;
using EcoTrack.Models;

namespace EcoTrack.Services;

public static class Catalogos
{
    private static readonly Dictionary<string, (TipoVehiculo Valor, string Etiqueta)> Tipos = new()
    {
        ["camion"] = (TipoVehiculo.Camion, "Camión"),
        ["furgoneta"] = (TipoVehiculo.Furgoneta, "Furgoneta"),
        ["camioneta"] = (TipoVehiculo.Camioneta, "Camioneta"),
        ["otro"] = (TipoVehiculo.Otro, "Otro"),
    };

    private static readonly Dictionary<string, (TipoCombustible Valor, string Etiqueta)> Combustibles = new()
    {
        ["diesel"] = (TipoCombustible.Diesel, "Diésel"),
        ["gasolina"] = (TipoCombustible.Gasolina, "Gasolina"),
        ["hibrido"] = (TipoCombustible.Hibrido, "Híbrido"),
        ["electrico"] = (TipoCombustible.Electrico, "Eléctrico"),
        ["glp"] = (TipoCombustible.Glp, "GLP"),
        ["otro"] = (TipoCombustible.Otro, "Otro"),
    };

    private static readonly Dictionary<string, (EstadoVehiculo Valor, string Etiqueta)> Estados = new()
    {
        ["activo"] = (EstadoVehiculo.Activo, "Activo"),
        ["mantenimiento"] = (EstadoVehiculo.Mantenimiento, "Mantenimiento"),
        ["inactivo"] = (EstadoVehiculo.Inactivo, "Inactivo"),
    };

    public static CatalogosRespuesta Todos() => new(
        Tipos.Select(t => new OpcionCatalogo(t.Key, t.Value.Etiqueta)).ToList(),
        Combustibles.Select(c => new OpcionCatalogo(c.Key, c.Value.Etiqueta)).ToList(),
        Estados.Select(e => new OpcionCatalogo(e.Key, e.Value.Etiqueta)).ToList(),
        PlacaSalvadorena.Clases.Select(c => new OpcionCatalogo(c.Key, c.Value)).ToList());

    public static TipoVehiculo? LeerTipo(string? valor, string campo, List<ErrorDetalle> errores) =>
        Leer(Tipos, valor, campo, "tipo de vehículo", errores);

    public static TipoCombustible? LeerCombustible(string? valor, string campo, List<ErrorDetalle> errores) =>
        Leer(Combustibles, valor, campo, "combustible", errores);

    public static EstadoVehiculo? LeerEstado(string? valor, string campo, List<ErrorDetalle> errores) =>
        Leer(Estados, valor, campo, "estado", errores);

    private static T? Leer<T>(
        Dictionary<string, (T Valor, string Etiqueta)> catalogo, string? valor, string campo, string nombre, List<ErrorDetalle> errores)
        where T : struct
    {
        if (valor is not null && catalogo.TryGetValue(valor.Trim().ToLowerInvariant(), out var encontrado))
        {
            return encontrado.Valor;
        }

        errores.Add(new ErrorDetalle(campo, $"Elige un {nombre} válido."));
        return null;
    }
}
