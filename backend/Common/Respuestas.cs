namespace EcoTrack.Common;

public sealed record RespuestaApi<T>(T Data, object? Meta = null, object? Links = null);

public sealed record ConteosPorEstado(int Activo, int Mantenimiento, int Inactivo, int Todos);

public sealed record MetaPaginacion(int Page, int Limit, int Total, int TotalPages, ConteosPorEstado? Conteos = null);

public sealed record EnlacesPaginacion(string Self, string? Next);

public static class FechasElSalvador
{
    private static readonly TimeSpan Desfase = TimeSpan.FromHours(-6);

    public static DateOnly Hoy() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Desfase).DateTime);
}
