using PgAiTrading.Models;

namespace PgAiTrading.Services;

public interface IIntradayCprService
{
    /// <summary>
    /// Builds intraday CPR windows from reference candles
    /// (15m bars → 15-minute segments; 1H bars → 60-minute segments on a 5m chart).
    /// </summary>
    IReadOnlyList<IntradayCprSegment> BuildSegments(
        List<Candle> referenceCandles,
        DateTime sessionDate,
        int segmentMinutes = 15);

    IntradayCprSegment? GetActiveSegment(IReadOnlyList<IntradayCprSegment> segments, DateTime time);
}

public class IntradayCprService : IIntradayCprService
{
    public const int DefaultSegmentMinutes = 15;
    /// <summary>5m chart CPR refresh interval — same as PGCryptoTrading.</summary>
    public const int FiveMinuteChartSegmentMinutes = 60;

    public IReadOnlyList<IntradayCprSegment> BuildSegments(
        List<Candle> referenceCandles,
        DateTime sessionDate,
        int segmentMinutes = DefaultSegmentMinutes)
    {
        if (referenceCandles.Count == 0 || segmentMinutes <= 0)
            return Array.Empty<IntradayCprSegment>();

        var ordered = referenceCandles.OrderBy(c => c.Timestamp).ToList();
        var segments = new List<IntradayCprSegment>();
        var sessionOpen = sessionDate.Date.Add(MarketHours.OpenTime);
        var sessionClose = sessionDate.Date.Add(MarketHours.CloseTime);

        var prevSessionCandle = ordered
            .Where(c => c.Timestamp < sessionOpen)
            .OrderByDescending(c => c.Timestamp)
            .FirstOrDefault();

        if (prevSessionCandle is not null)
        {
            var firstEnd = sessionOpen.AddMinutes(segmentMinutes);
            if (firstEnd <= sessionClose)
                segments.Add(MakeSegment(sessionOpen, firstEnd, prevSessionCandle));
        }

        var boundary = sessionOpen.AddMinutes(segmentMinutes);
        while (boundary < sessionClose)
        {
            var refOpen = boundary.AddMinutes(-segmentMinutes);
            var refCandle = FindReferenceCandle(ordered, refOpen, segmentMinutes) ?? prevSessionCandle;
            if (refCandle is not null)
                segments.Add(MakeSegment(boundary, boundary.AddMinutes(segmentMinutes), refCandle));

            boundary = boundary.AddMinutes(segmentMinutes);
        }

        return segments;
    }

    public IntradayCprSegment? GetActiveSegment(IReadOnlyList<IntradayCprSegment> segments, DateTime time)
    {
        foreach (var segment in segments)
        {
            if (time >= segment.Start && time < segment.End)
                return segment;
        }

        return segments.LastOrDefault();
    }

    private static Candle? FindReferenceCandle(List<Candle> ordered, DateTime openTime, int segmentMinutes)
    {
        var match = ordered.FirstOrDefault(c => c.Timestamp == openTime);
        if (match is not null)
            return match;

        var lookbackMinutes = segmentMinutes + 5;
        return ordered
            .Where(c => c.Timestamp <= openTime && c.Timestamp > openTime.AddMinutes(-lookbackMinutes))
            .OrderByDescending(c => c.Timestamp)
            .FirstOrDefault();
    }

    private static IntradayCprSegment MakeSegment(DateTime start, DateTime end, Candle reference)
    {
        var pivot = (reference.High + reference.Low + reference.Close) / 3m;
        var bc = (reference.High + reference.Low) / 2m;
        var tc = 2m * pivot - bc;
        return new IntradayCprSegment(start, end, Round(pivot), Round(tc), Round(bc));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2);
}
