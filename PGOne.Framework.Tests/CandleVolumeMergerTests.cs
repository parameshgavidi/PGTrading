using PGOne.Models;

namespace PGOne.Framework.Tests;

public class CandleVolumeMergerTests
{
    [Fact]
    public void MergeVolume_copies_futures_volume_onto_index_candles_by_timestamp()
    {
        var barTime = new DateTime(2026, 7, 28, 10, 15, 37);
        var index = new List<Candle>
        {
            new()
            {
                Timestamp = barTime,
                Open = 100m,
                High = 101m,
                Low = 99m,
                Close = 100.5m,
                Volume = 0
            }
        };

        var future = new List<Candle>
        {
            new()
            {
                Timestamp = barTime,
                Open = 100m,
                High = 101m,
                Low = 99m,
                Close = 100.5m,
                Volume = 125_000
            }
        };

        var merged = CandleVolumeMerger.CopyWithVolumeFrom(index, future);

        Assert.Equal(125_000, merged[0].Volume);
        Assert.Equal(100.5m, merged[0].Close);
    }

    [Fact]
    public void HasTradeableVolume_false_when_all_zero()
    {
        var candles = new List<Candle>
        {
            new() { Volume = 0 },
            new() { Volume = 0 }
        };

        Assert.False(CandleVolumeMerger.HasTradeableVolume(candles));
    }
}
