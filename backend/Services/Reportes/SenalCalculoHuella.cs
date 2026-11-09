using System.Threading.Channels;

namespace EcoTrack.Services.Reportes;

public sealed class SenalCalculoHuella
{
    private readonly Channel<bool> canal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Notificar() => canal.Writer.TryWrite(true);

    public async Task EsperarAsync(TimeSpan maximo, CancellationToken cancelacion)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        limite.CancelAfter(maximo);
        try
        {
            await canal.Reader.ReadAsync(limite.Token);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
        }
    }
}
