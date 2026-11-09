using EcoTrack.Data;
using EcoTrack.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EcoTrack.Services.Reportes;

public sealed partial class ProcesadorHuella(
    IServiceScopeFactory fabrica, SenalCalculoHuella senal, ILogger<ProcesadorHuella> logger) : BackgroundService
{
    private const int IntentosMaximos = 3;
    private static readonly TimeSpan EsperaEntreIntentos = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RevisionPeriodica = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var recuperado = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!recuperado)
                {
                    await RecuperarInterrumpidosAsync(stoppingToken);
                    recuperado = true;
                }

                await ProcesarPendientesAsync(stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                RegistrarFalloGeneral(logger, error);
            }

            await senal.EsperarAsync(RevisionPeriodica, stoppingToken);
        }
    }

    private async Task RecuperarInterrumpidosAsync(CancellationToken cancelacion)
    {
        await using var alcance = fabrica.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.ReporteVersiones
            .Where(v => v.EstadoCalculo == EstadoCalculo.Procesando)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.EstadoCalculo, EstadoCalculo.Pendiente), cancelacion);
    }

    private async Task ProcesarPendientesAsync(CancellationToken cancelacion)
    {
        List<Guid> pendientes;
        await using (var alcance = fabrica.CreateAsyncScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
            pendientes = await db.ReporteVersiones.AsNoTracking()
                .Where(v => v.EstadoCalculo == EstadoCalculo.Pendiente)
                .OrderBy(v => v.CreadoEn)
                .Select(v => v.ReporteVersionId)
                .Take(10)
                .ToListAsync(cancelacion);
        }

        foreach (var versionId in pendientes)
        {
            await ProcesarAsync(versionId, cancelacion);
        }
    }

    private async Task ProcesarAsync(Guid versionId, CancellationToken cancelacion)
    {
        await using var alcance = fabrica.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        var calculador = alcance.ServiceProvider.GetRequiredService<CalculadorHuella>();

        var tomada = await db.ReporteVersiones
            .Where(v => v.ReporteVersionId == versionId && v.EstadoCalculo == EstadoCalculo.Pendiente)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.EstadoCalculo, EstadoCalculo.Procesando)
                .SetProperty(v => v.CalculoIniciadoEn, DateTimeOffset.UtcNow), cancelacion);
        if (tomada == 0)
        {
            return;
        }

        for (var intento = 1; ; intento++)
        {
            try
            {
                await calculador.CalcularAsync(versionId, cancelacion);
                return;
            }
            catch (Exception error) when (intento < IntentosMaximos && EsTransitorio(error))
            {
                RegistrarReintento(logger, versionId, intento, error);
                db.ChangeTracker.Clear();
                await Task.Delay(EsperaEntreIntentos, cancelacion);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                RegistrarFalloCalculo(logger, versionId, error);
                await calculador.MarcarFallidoAsync(versionId,
                    "No se pudo calcular la huella por un error del servidor. Reprocesa la versión.", cancelacion);
                return;
            }
        }
    }

    private static bool EsTransitorio(Exception error) =>
        error is IOException or TimeoutException or NpgsqlException { IsTransient: true }
        || error.InnerException is NpgsqlException { IsTransient: true };

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo al revisar los cálculos de huella pendientes")]
    private static partial void RegistrarFalloGeneral(ILogger logger, Exception error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reintento {Intento} del cálculo de huella de la versión {VersionId}")]
    private static partial void RegistrarReintento(ILogger logger, Guid versionId, int intento, Exception error);

    [LoggerMessage(Level = LogLevel.Error, Message = "El cálculo de huella de la versión {VersionId} falló")]
    private static partial void RegistrarFalloCalculo(ILogger logger, Guid versionId, Exception error);
}
