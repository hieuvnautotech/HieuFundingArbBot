namespace HieuFundingArbBot.Models
{
    public class FundingRate
    {
        public required string Exchange { get; set; }
        public required string Symbol { get; set; }

        public double Rate { get; set; }          // funding rate
        public DateTime Timestamp { get; set; }   // when received

        public string Source { get; set; } = "REST";   // REST / WS
        public bool IsRealTime => Source == "WS";

        public override string ToString()
        {
            return $"{Exchange} {Symbol} → {Rate} @ {Timestamp:HH:mm:ss} [{Source}]";
        }
    }
}