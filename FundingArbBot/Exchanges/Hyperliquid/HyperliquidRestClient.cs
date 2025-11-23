using System.Net.Http;
using System.Text;
using System.Text.Json;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Models;
using HieuFundingArbBot.Interfaces;

namespace HieuFundingArbBot.Exchanges.Hyperliquid
{
    public class HyperliquidRestClient : IExchangeClient
    {
        private readonly HttpClient _http;
        private readonly SimpleLogger _logger;

        public string Name => "Hyperliquid";

        public HyperliquidRestClient(string apiKey, string secret, SimpleLogger logger)
        {
            _logger = logger;

            _http = new HttpClient
            {
                BaseAddress = new Uri("https://api.hyperliquid.xyz")
            };
        }

        public async Task<FundingRate> GetFundingRateAsync(string symbol)
        {
            try
            {
                var body = new
                {
                    type = "activeAssetCtx",
                    coin = "BTC"
                };

                var req = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json"
                );

                var res = await _http.PostAsync("/info", req);
                var json = await res.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);

                // JSON format:
                // [
                //   "activeAssetCtx",
                //   {
                //     "coin":"BTC",
                //     "ctx": { "funding": 0.00001 ... }
                //   }
                // ]
                var ctx = doc.RootElement[1]
                             .GetProperty("ctx");

                double funding = ctx
                                 .GetProperty("funding")
                                 .GetDouble();

                return new FundingRate
                {
                    Exchange = "Hyperliquid",
                    Symbol = symbol,
                    Rate = funding,
                    Timestamp = DateTime.UtcNow,
                    Source = "REST"
                };
            }
            catch (Exception ex)
            {
                _logger.Error("[HL REST] " + ex.Message);

                return new FundingRate
                {
                    Exchange = "Hyperliquid",
                    Symbol = symbol,
                    Rate = 0,
                    Timestamp = DateTime.UtcNow,
                    Source = "REST-ERR"
                };
            }
        }
    }
}
