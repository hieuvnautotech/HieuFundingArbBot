using HieuFundingArbBot.Interfaces;
using HieuHieuFundingArbBot.Models;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Exchanges.Hyperliquid;
using HieuFundingArbBot.Models;

namespace HieuFundingArbBot.Exchanges
{
    // Wrapper / adapter - uses HyperliquidRestClient internally
    public class HyperliquidClient : IExchangeClient
    {
        private readonly SimpleLogger _logger;
        private readonly HyperliquidRestClient _rest;

        public string Name { get; }

        public HyperliquidClient(string name, SimpleLogger logger)
        {
            Name = name;
            _logger = logger;

            // Nếu muốn truyền credentials, thay "" bằng config values
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

        // Nếu IExchangeClient yêu cầu PlaceOrder/CancelOrder, implement chuyển tiếp tới _rest
        // (các method dưới là tùy project; xóa nếu interface không cần)
        public Task<OrderResult> PlaceOrderAsync(OrderRequest req)
            => _rest.PlaceOrderAsync(req);

        public Task<OrderResult> CancelOrderAsync(string orderId)
            => _rest.CancelOrderAsync(orderId);
    }
}
