using Amazon;
using Amazon.S3;

namespace EcoTrack.Services.Almacenamiento;

public static class RegistroAlmacenamiento
{
    public static IServiceCollection AgregarAlmacenamiento(
        this IServiceCollection servicios, IConfiguration configuracion, string raizContenido)
    {
        var tipo = (configuracion["ALMACENAMIENTO"] ?? "local").Trim().ToLowerInvariant();
        if (tipo == "local")
        {
            var ruta = Path.Combine(raizContenido, configuracion["ALMACENAMIENTO_LOCAL_RUTA"] ?? "bucket-local");
            return servicios.AddSingleton<IAlmacenamientoArchivos>(new AlmacenamientoLocal(ruta));
        }

        if (tipo != "s3")
        {
            throw new InvalidOperationException("ALMACENAMIENTO debe ser local o s3");
        }

        var bucket = configuracion["S3_BUCKET"];
        if (string.IsNullOrWhiteSpace(bucket))
        {
            throw new InvalidOperationException("Falta S3_BUCKET");
        }

        var region = RegionEndpoint.GetBySystemName(configuracion["AWS_REGION"] ?? "us-east-1");
        var opciones = new AmazonS3Config { RegionEndpoint = region };
        var servicioPropio = configuracion["S3_SERVICE_URL"];
        if (!string.IsNullOrWhiteSpace(servicioPropio))
        {
            opciones.ServiceURL = servicioPropio;
            opciones.AuthenticationRegion = region.SystemName;
            opciones.ForcePathStyle = true;
        }

        servicios.AddSingleton<IAmazonS3>(new AmazonS3Client(opciones));
        return servicios.AddSingleton<IAlmacenamientoArchivos>(proveedor =>
            new AlmacenamientoS3(proveedor.GetRequiredService<IAmazonS3>(), bucket.Trim()));
    }
}
