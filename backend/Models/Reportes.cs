namespace EcoTrack.Models;

public enum EstadoReporte
{
    Generado,
    Subido,
    Calculado,
    EnAuditoria,
    Auditado,
    Fallido,
}

public enum OrigenVersion
{
    Original,
    Corregida,
}

public enum EstadoCalculo
{
    Pendiente,
    Procesando,
    Completado,
    Fallido,
}

public enum EstadoDetalleHuella
{
    Ok,
    Error,
}

public class ReporteViaje
{
    public Guid ReporteViajeId { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public DateOnly PeriodoDesde { get; set; }
    public DateOnly PeriodoHasta { get; set; }
    public string Filtros { get; set; } = "{}";
    public int TotalFilas { get; set; }
    public EstadoReporte Estado { get; set; }
    public string? MensajeError { get; set; }
    public Guid GeneradoPorUsuarioId { get; set; }
    public Usuario? GeneradoPor { get; set; }
    public DateTimeOffset GeneradoEn { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; }
    public List<ReporteVersion> Versiones { get; } = [];
}

public class ReporteVersion
{
    public Guid ReporteVersionId { get; set; }
    public Guid ReporteViajeId { get; set; }
    public ReporteViaje? Reporte { get; set; }
    public int NumeroVersion { get; set; }
    public OrigenVersion Origen { get; set; }
    public string ClaveAlmacenamiento { get; set; } = string.Empty;
    public string? S3VersionId { get; set; }
    public string HashSha256 { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public int TotalFilas { get; set; }
    public Guid? VersionBaseId { get; set; }
    public EstadoCalculo EstadoCalculo { get; set; }
    public string? MensajeError { get; set; }
    public DateTimeOffset? CalculoIniciadoEn { get; set; }
    public DateTimeOffset? CalculoTerminadoEn { get; set; }
    public bool EsOficial { get; set; }
    public Guid CreadoPorUsuarioId { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public HuellaReporte? Huella { get; set; }
}

public class HuellaReporte
{
    public Guid HuellaReporteId { get; set; }
    public Guid ReporteVersionId { get; set; }
    public int FilasProcesadas { get; set; }
    public int FilasValidas { get; set; }
    public int FilasConError { get; set; }
    public decimal DistanciaTotalKm { get; set; }
    public decimal Co2TotalKg { get; set; }
    public string? ClaveResultado { get; set; }
    public DateTimeOffset CalculadoEn { get; set; }
    public List<HuellaReporteDetalle> Detalle { get; } = [];
}

public class HuellaReporteDetalle
{
    public long HuellaReporteDetalleId { get; set; }
    public Guid HuellaReporteId { get; set; }
    public int FilaNumero { get; set; }
    public string? ViajeReferencia { get; set; }
    public string? VehiculoPlaca { get; set; }
    public string? TipoVehiculo { get; set; }
    public string? TipoCombustible { get; set; }
    public DateTimeOffset? SalidaEn { get; set; }
    public decimal? DistanciaKm { get; set; }
    public Guid? FactorEmisionId { get; set; }
    public decimal? Co2Kg { get; set; }
    public decimal? Co2KgOperativo { get; set; }
    public EstadoDetalleHuella Estado { get; set; }
    public string? MensajeError { get; set; }
}
