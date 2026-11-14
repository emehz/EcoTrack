namespace EcoTrack.Models;

public class Sesion
{
    public Guid SesionId { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public Guid FamiliaId { get; set; }
    public string HashToken { get; set; } = string.Empty;
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset ExpiraEn { get; set; }
    public DateTimeOffset? UsadoEn { get; set; }
    public DateTimeOffset? RevocadoEn { get; set; }
    public string? Ip { get; set; }
    public string? Agente { get; set; }
}
