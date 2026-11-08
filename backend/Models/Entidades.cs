namespace EcoTrack.Models;

public class Usuario
{
    public Guid UsuarioId { get; set; }
    public string Correo { get; set; } = string.Empty;
    public string HashContrasena { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; }
}

public class FactorEmision
{
    public Guid FactorEmisionId { get; set; }
    public TipoCombustible TipoCombustible { get; set; }
    public TipoVehiculo TipoVehiculo { get; set; }
    public decimal FactorCo2Km { get; set; }
    public DateOnly VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public string? Fuente { get; set; }
}

public class Vehiculo
{
    public Guid VehiculoId { get; set; }
    public string Placa { get; set; } = string.Empty;
    public TipoVehiculo TipoVehiculo { get; set; }
    public TipoCombustible TipoCombustible { get; set; }
    public EstadoVehiculo Estado { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; }
}

public class Viaje
{
    public Guid ViajeId { get; set; }
    public Guid VehiculoId { get; set; }
    public Vehiculo? Vehiculo { get; set; }
    public DateTimeOffset SalidaEn { get; set; }
    public decimal DistanciaKm { get; set; }
    public decimal Co2Kg { get; set; }
    public EstadoViaje Estado { get; set; }
}

public class CargaCombustible
{
    public Guid CargaCombustibleId { get; set; }
    public Guid VehiculoId { get; set; }
    public Vehiculo? Vehiculo { get; set; }
    public DateTimeOffset CargadoEn { get; set; }
    public decimal Litros { get; set; }
}
