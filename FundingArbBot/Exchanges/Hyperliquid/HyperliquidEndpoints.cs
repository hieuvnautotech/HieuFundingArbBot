namespace FundingArbBot.Exchanges.Hyperliquid
{
    public static class HyperliquidEndpoints
    {
        public const string BaseRest = "https://api.hyperliquid.xyz";
        public const string Funding = "/funding";              // TODO: replace with real endpoint
        public const string Ticker = "/ticker";                // TODO
        public const string Order = "/order";                  // TODO
        public const string Positions = "/positions";          // TODO

        public const string WsUrl = "wss://api.hyperliquid.xyz/ws"; // TODO: replace real WS endpoint
    }
}