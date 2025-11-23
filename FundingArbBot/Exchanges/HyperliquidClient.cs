using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Models;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Exchanges.Hyperliquid;

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

            // dùng API thật — mày có thể truyền key từ Config
            _rest = new HyperliquidRestClient(
                apiKey: "",
                secret: "",
                logger
            );
        }

        public async Task<FundingRate> GetFundingRateAsync(string symbol)
        {
            var fr = await _rest.GetFundingRateAsync(symbol);

            if (fr == null)
            {
                _logger.Warn("[HL-REST] Funding null → dùng fallback 0");
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
    }
}
