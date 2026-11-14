namespace EcoTrack.Models;

public class RegistroBitacora
{
    public long AuditoriaId { get; set; }
    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public Guid? EntidadId { get; set; }
    public string? DatosAnteriores { get; set; }
    public string? DatosNuevos { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
