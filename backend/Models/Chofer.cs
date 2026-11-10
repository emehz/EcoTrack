namespace EcoTrack.Models;

public enum EstadoChofer
{
    Activo,
    Inactivo,
    Suspendido,
}

public class Chofer
{
    public Guid ChoferId { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string NumeroLicencia { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public EstadoChofer Estado { get; set; }
}
