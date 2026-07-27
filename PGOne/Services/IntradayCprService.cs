using PGOne.Models;

namespace PGOne.Services;

public interface IIntradayCprService
{
    IReadOnlyList<IntradayCprSegment> BuildSegments(List<Candle> candles15m, DateTime sessionDate);
    IntradayCprSegment? GetActiveSegment(IReadOnlyList<IntradayCprSegment> segments, DateTime time);
}

public class IntradayCprService : IIntradayCprService
{
    public IReadOnlyList<IntradayCprSegment> BuildSegments(List<Candle> candles15m, DateTime sessionDate)
    {
        if (candles15m.Count == 0)
            return Array.Empty<IntradayCprSegment>();

        var ordered = candles15m.OrderBy(c => c.Timestamp).ToList();
        var segments = new List<IntradayCprSegment>();
        var sessionOpen = sessionDate.Date.Add(MarketHours.OpenTime);
        var sessionClose = sessionDate.Date.Add(MarketHours.CloseTime);

        var prevSessionCandle = ordered
            .Where(c => c.Timestamp < sessionOpen)
            .OrderByDescending(c => c.Timestamp)
            .FirstOrDefault();

        if (prevSessionCandle is not null)
        {
            var firstEnd = sessionOpen.AddMinutes(15);
            if (firstEnd <= sessionClose)
                segments.Add(MakeSegment(sessionOpen, firstEnd, prevSessionCandle));
        }

        var boundary = sessionOpen.AddMinutes(15);
        while (boundary < sessionClose)
        {
            var refOpen = boundary.AddMinutes(-15);
            var refCandle = Find15mCandle(ordered, refOpen) ?? prevSessionCandle;
            if (refCandle is not null)
                segments.Add(MakeSegment(boundary, boundary.AddMinutes(15), refCandle));

            boundary = boundary.AddMinutes(15);
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

    private static Candle? Find15mCandle(List<Candle> ordered, DateTime openTime)
    {
        var match = ordered.FirstOrDefault(c => c.Timestamp == openTime);
        if (match is not null)
            return match;

        return ordered
            .Where(c => c.Timestamp <= openTime && c.Timestamp > openTime.AddMinutes(-20))
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
