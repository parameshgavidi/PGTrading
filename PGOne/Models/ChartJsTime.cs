namespace PGOne.Models;

/// <summary>IST wall-clock timestamps as epoch ms for chart.js segment matching.</summary>
public static class ChartJsTime
{
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    public static long ToEpochMs(DateTime timestamp)
    {
        var dto = new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified), IstOffset);
        return dto.ToUnixTimeMilliseconds();
    }
}
