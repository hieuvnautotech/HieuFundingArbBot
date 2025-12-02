namespace FundingArbBot.Models
{
    public class OrderRequest
    {
        public string Symbol { get; set; } = "BTC-PERP";
        public string Side { get; set; } = "buy"; // buy / sell
        public double SizeUsd { get; set; } // size in USD
        public double Price { get; set; } = 0; // 0 for market
        public bool Market => Price == 0;
        public bool ReduceOnly { get; set; } = false;
    }
}