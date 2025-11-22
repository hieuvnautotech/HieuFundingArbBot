namespace HieuFundingArbBot.Models
{
    public class FundingRate
    {
        public string Exchange { get; set; }
        public string Symbol { get; set; } = "BTC-PERP";
        public double Rate { get; set; } // e.g. 0.0002 means 0.02%
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}