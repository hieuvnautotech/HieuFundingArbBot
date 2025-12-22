// HieuFundingArbBot/Models/OrderResult.cs
namespace HieuHieuFundingArbBot.Models
{
    public class OrderResult
    {
        // Unique order id (exchange or simulated)
        public string OrderId { get; set; } = "";

        // "filled", "cancelled", "open", etc.
        public string Status { get; set; } = "";

        // Filled size in USD (or 0 if none)
        public double FilledSizeUsd { get; set; }

        // Average filled price (or 0)
        public double AvgPrice { get; set; }

        // Raw JSON string returned by exchange (for debugging)
        public string Raw { get; set; } = "";

        // New: generic success flag + message used by some REST adapters
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "";
    }
}
