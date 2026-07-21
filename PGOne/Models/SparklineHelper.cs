namespace PGOne.Models;

public static class SparklineHelper
{
    private const string Blocks = "▁▂▃▄▅▆▇█";

    public static string Generate(TrendDirection trend, decimal changePercent)
    {
        var chars = new char[6];
        var magnitude = Math.Clamp((int)(Math.Abs(changePercent) * 3), 0, 3);

        for (var i = 0; i < chars.Length; i++)
        {
            var level = trend switch
            {
                TrendDirection.Buy => 2 + i / 2 + magnitude / 2,
                TrendDirection.Sell => 6 - i / 2 - magnitude / 2,
                _ => 3 + (i % 2)
            };
            chars[i] = Blocks[Math.Clamp(level, 0, Blocks.Length - 1)];
        }

        return new string(chars);
    }
}
