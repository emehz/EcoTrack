namespace EcoTrack.Models;

public enum RolUsuario
{
    Admin,
    Operador,
    Auditor,
}

public enum TipoVehiculo
{
    Camion,
    Furgoneta,
    Camioneta,
    Otro,
}

public enum TipoCombustible
{
    Diesel,
    Gasolina,
    Hibrido,
    Electrico,
    Glp,
    Otro,
}

public enum EstadoVehiculo
{
    Activo,
    Mantenimiento,
    Inactivo,
}

public enum EstadoViaje
{
    Planificado,
    EnCurso,
    Completado,
    Cancelado,
}
