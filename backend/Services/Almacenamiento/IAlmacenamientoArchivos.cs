namespace EcoTrack.Services.Almacenamiento;

public interface IAlmacenamientoArchivos
{
    Task GuardarAsync(string clave, byte[] contenido, bool permitirReemplazo, CancellationToken cancelacion);

    Task<byte[]?> LeerAsync(string clave, CancellationToken cancelacion);
}
