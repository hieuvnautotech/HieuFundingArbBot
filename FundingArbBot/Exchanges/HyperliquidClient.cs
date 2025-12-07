using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Exchanges.Hyperliquid;
using HieuFundingArbBot.Models;
using System.Threading.Tasks;
using FundingArbBot.Models;
using FundingArbBot.Exchanges.Hyperliquid;

namespace HieuFundingArbBot.Exchanges
{
    public class HyperliquidClient : IExchangeClient
    {
        private readonly SimpleLogger _logger;
        private readonly HyperliquidRestClient _rest;

        public string Name { get; }

        public HyperliquidClient(string name, SimpleLogger logger)
        {
            Name = name;
            _logger = logger;
            _rest = new HyperliquidRestClient(apiKey: "", secret: "", logger);
        }

        public async Task<FundingRate> GetFundingRateAsync(string symbol)
        {
            var fr = await _rest.GetFundingRateAsync(symbol);
            if (fr == null)
            {
                _logger.Warn("[HL-REST] Funding null → fallback 0");
                return new FundingRate
                {
                    Exchange = "Hyperliquid",
                    Symbol = symbol,
                    Rate = 0,
                    Timestamp = DateTime.UtcNow,
                    Source = "REST"
                };
            }
            return fr;
        }

        // Forward order methods to the REST adapter (currently simulated)
        public Task<OrderResult> PlaceOrderAsync(OrderRequest req) => _rest.PlaceOrderAsync(req);

        public Task<OrderResult> CancelOrderAsync(string orderId) => _rest.CancelOrderAsync(orderId);
    }
}
