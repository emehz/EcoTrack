using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace EcoTrack.Services.Almacenamiento;

public sealed class AlmacenamientoS3(IAmazonS3 cliente, string bucket) : IAlmacenamientoArchivos
{
    public async Task GuardarAsync(string clave, byte[] contenido, bool permitirReemplazo, CancellationToken cancelacion)
    {
        ClavesAlmacenamiento.Validar(clave);
        using var datos = new MemoryStream(contenido, writable: false);
        var solicitud = new PutObjectRequest
        {
            BucketName = bucket,
            Key = clave,
            InputStream = datos,
            ContentType = clave.EndsWith(".json", StringComparison.Ordinal) ? "application/json" : "text/csv; charset=utf-8",
            UseChunkEncoding = false,
        };
        if (!permitirReemplazo)
        {
            solicitud.IfNoneMatch = "*";
        }

        try
        {
            await cliente.PutObjectAsync(solicitud, cancelacion);
        }
        catch (AmazonS3Exception error) when (error.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new IOException($"Ya existe un archivo con la clave {clave}; las versiones no se sobrescriben.", error);
        }
    }

    public async Task<byte[]?> LeerAsync(string clave, CancellationToken cancelacion)
    {
        ClavesAlmacenamiento.Validar(clave);
        try
        {
            using var respuesta = await cliente.GetObjectAsync(bucket, clave, cancelacion);
            using var destino = new MemoryStream();
            await respuesta.ResponseStream.CopyToAsync(destino, cancelacion);
            return destino.ToArray();
        }
        catch (AmazonS3Exception error) when (error.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
