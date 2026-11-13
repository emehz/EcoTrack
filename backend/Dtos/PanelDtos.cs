namespace EcoTrack.Dtos;

public sealed record PanelFiltro(DateOnly? Desde, DateOnly? Hasta, string? TipoVehiculo, string? TipoCombustible, int Top);

public sealed record PeriodoPanel(DateOnly Desde, DateOnly Hasta);

public sealed record CifrasPanel(int Viajes, decimal Km, decimal Co2Kg, decimal Co2Toneladas, decimal? IntensidadKgKm);

public sealed record FlotaPanel(int VehiculosActivos, int ElectricosHibridosActivos);

public sealed record EmisionPorGrupo(string Grupo, int Viajes, decimal Co2Kg);

public sealed record VehiculoEmisor(
    Guid VehiculoId, string Placa, string TipoVehiculo, string TipoCombustible, int Viajes, decimal Km, decimal Co2Kg);

public sealed record MesPanel(string Mes, decimal Co2Kg);

public sealed record PanelRespuesta(
    PeriodoPanel Periodo,
    CifrasPanel Resumen,
    PeriodoPanel PeriodoAnterior,
    CifrasPanel Anterior,
    decimal? VariacionIntensidadPct,
    CifrasPanel AcumuladoAnio,
    FlotaPanel Flota,
    IReadOnlyList<EmisionPorGrupo> PorCombustible,
    IReadOnlyList<EmisionPorGrupo> PorTipoVehiculo,
    IReadOnlyList<VehiculoEmisor> TopVehiculos,
    IReadOnlyList<MesPanel> Tendencia);
