namespace EcoTrack.Services.Almacenamiento;

public sealed class AlmacenamientoLocal : IAlmacenamientoArchivos
{
    private readonly string raiz;

    public AlmacenamientoLocal(string ruta)
    {
        raiz = Path.GetFullPath(ruta);
        Directory.CreateDirectory(Path.Combine(raiz, "reportes"));
        Directory.CreateDirectory(Path.Combine(raiz, "resultados"));
    }

    public async Task GuardarAsync(string clave, byte[] contenido, bool permitirReemplazo, CancellationToken cancelacion)
    {
        var destino = RutaFisica(clave);
        if (!permitirReemplazo && File.Exists(destino))
        {
            throw new IOException($"Ya existe un archivo con la clave {clave}; las versiones no se sobrescriben.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        var temporal = destino + ".tmp";
        await File.WriteAllBytesAsync(temporal, contenido, cancelacion);
        File.Move(temporal, destino, permitirReemplazo);
    }

    public async Task<byte[]?> LeerAsync(string clave, CancellationToken cancelacion)
    {
        var origen = RutaFisica(clave);
        return File.Exists(origen) ? await File.ReadAllBytesAsync(origen, cancelacion) : null;
    }

    private string RutaFisica(string clave)
    {
        ClavesAlmacenamiento.Validar(clave);
        var ruta = Path.GetFullPath(Path.Combine(raiz, clave));
        if (!ruta.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"La clave apunta fuera del almacenamiento: {clave}", nameof(clave));
        }

        return ruta;
    }
}
