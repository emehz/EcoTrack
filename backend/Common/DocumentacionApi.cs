using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EcoTrack.Common;

public sealed class DocumentacionApi : IOpenApiDocumentTransformer
{
    private const string Esquema = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "EcoTrack API",
            Version = "v1",
            Description = "API del panel EcoTrack: vehículos, choferes, viajes, combustible, factores de emisión, panel, " +
                "reportes con cálculo de huella, auditorías, usuarios y bitácora. Las respuestas usan snake_case y " +
                "los errores tienen la forma { error: { code, message, details, trace_id } }.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[Esquema] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token de acceso de POST /api/v1/auth/login o /api/v1/auth/refresh. Dura 15 minutos.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(Esquema, document)] = [],
        });
        return Task.CompletedTask;
    }
}
