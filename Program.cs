using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcoTrack.Common;
using EcoTrack.Data;
using EcoTrack.Models;
using EcoTrack.Services;
using EcoTrack.Services.Almacenamiento;
using EcoTrack.Services.Auditorias;
using EcoTrack.Services.Reportes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta ConnectionStrings__DefaultConnection");

var jwtOpciones = JwtOpciones.Desde(builder.Configuration);

builder.Services.AddSingleton(jwtOpciones);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.MapEnum<RolUsuario>("rol_usuario_enum", AppDbContext.Esquema);
        npgsql.MapEnum<TipoVehiculo>("tipo_vehiculo_enum", AppDbContext.Esquema);
        npgsql.MapEnum<TipoCombustible>("tipo_combustible_enum", AppDbContext.Esquema);
        npgsql.MapEnum<EstadoVehiculo>("estado_vehiculo_enum", AppDbContext.Esquema);
        npgsql.MapEnum<EstadoViaje>("estado_viaje_enum", AppDbContext.Esquema);
        npgsql.MapEnum<EstadoChofer>("estado_chofer_enum", AppDbContext.Esquema);
        npgsql.MapEnum<EstadoReporte>("estado_reporte_enum", AppDbContext.Esquema);
        npgsql.MapEnum<OrigenVersion>("origen_version_enum", AppDbContext.Esquema);
        npgsql.MapEnum<EstadoCalculo>("estado_carga_enum", AppDbContext.Esquema);
        npgsql.MapEnum<EstadoDetalleHuella>("estado_detalle_huella_enum", AppDbContext.Esquema);
        npgsql.MapEnum<EstadoAuditoria>("estado_auditoria_reporte_enum", AppDbContext.Esquema);
        npgsql.MapEnum<ResultadoAuditoria>("resultado_auditoria_enum", AppDbContext.Esquema);
        npgsql.MapEnum<SeveridadHallazgo>("severidad_hallazgo_enum", AppDbContext.Esquema);
        npgsql.MapEnum<EstadoHallazgo>("estado_hallazgo_enum", AppDbContext.Esquema);
        npgsql.MapEnum<OrigenHallazgo>("origen_hallazgo_enum", AppDbContext.Esquema);
    }));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IntentosLogin>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SesionVigente>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<VehiculoService>();
builder.Services.AddScoped<FactorEmisionService>();
builder.Services.AddScoped<FactorHistorialService>();
builder.Services.AddScoped<ChoferService>();
builder.Services.AddScoped<ViajeService>();

var rutaAlmacenamiento = Path.Combine(
    builder.Environment.ContentRootPath, builder.Configuration["ALMACENAMIENTO_LOCAL_RUTA"] ?? "bucket-local");
builder.Services.AddSingleton<IAlmacenamientoArchivos>(new AlmacenamientoLocal(rutaAlmacenamiento));

builder.Services.AddSingleton<SenalCalculoHuella>();
builder.Services.AddScoped<ReporteService>();
builder.Services.AddScoped<CalculadorHuella>();
builder.Services.AddHostedService<ProcesadorHuella>();
builder.Services.AddSingleton(ParametrosAuditoria.Desde(builder.Configuration));
builder.Services.AddScoped<ControlesAuditoria>();
builder.Services.AddScoped<AuditoriaReporteService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = context.ModelState
            .Where(entrada => entrada.Value is { Errors.Count: > 0 })
            .Select(entrada => new ErrorDetalle(entrada.Key, "El valor enviado no es válido."))
            .ToList();
        return new BadRequestObjectResult(RespuestaError.Crear(
            CodigosError.SolicitudInvalida, "La solicitud no es válida.", context.HttpContext, details));
    });

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOpciones.Emisor,
            ValidateAudience = true,
            ValidAudience = jwtOpciones.Audiencia,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpciones.Secreto)),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = TokenService.ClaimRol,
            NameClaimType = TokenService.ClaimSujeto,
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async contexto =>
            {
                var servicios = contexto.HttpContext.RequestServices;
                var vigente = await servicios.GetRequiredService<SesionVigente>().EsValidaAsync(
                    contexto.Principal!, servicios.GetRequiredService<AppDbContext>(), contexto.HttpContext.RequestAborted);
                if (!vigente)
                {
                    contexto.Fail("La sesión ya no es válida.");
                }
            },
        };
    });
builder.Services.AddAuthorization();

var origenes = (builder.Configuration["CORS_ORIGINS"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorErrores>();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

app.UseExceptionHandler();
app.UseStatusCodePages(ManejadorErrores.EscribirEstadoAsync);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
