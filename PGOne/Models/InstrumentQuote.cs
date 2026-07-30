namespace PGOne.Models;

public class InstrumentQuote
{
    public decimal LastPrice { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal PreviousClose { get; set; }

    public decimal ChangePercent
    {
        get
        {
            var reference = PreviousClose > 0 ? PreviousClose : Open;
            return reference > 0
                ? Math.Round((LastPrice - reference) / reference * 100, 2)
                : 0m;
        }
    }

    public decimal Change => PreviousClose > 0
        ? LastPrice - PreviousClose
        : LastPrice - Open;
}
