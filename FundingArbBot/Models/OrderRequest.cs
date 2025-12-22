namespace HieuFundingArbBot.Models
{
    public class OrderRequest
    {
        // "buy" / "sell"
        public string Side { get; set; } = "";

        // "BTC-PERP"
        public string Symbol { get; set; } = "";

        // optional exchange-specific market
        public string? Market { get; set; }

        // size in USD
        public double SizeUsd { get; set; }

        // optional client id
        public string? ClientOrderId { get; set; }

        // "market" | "limit"
        public string OrderType { get; set; } = "market";

        // limit price (optional)
        public double? Price { get; set; }
    }
}
