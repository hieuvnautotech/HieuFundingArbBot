using System.Net.Http;
using System.Text.Json;
using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Models;

namespace HieuFundingArbBot.Exchanges.Lighter
{
    public class LighterRealClient : IExchangeClient
    {
        private readonly HttpClient _http = new HttpClient();
        private readonly SimpleLogger _logger;

        public string Name => "Lighter";

        public LighterRealClient(SimpleLogger logger)
        {
            _logger = logger;
        }

        public async Task<FundingRate> GetFundingRateAsync(string symbol)
        {
            try
            {
                var url = "https://mainnet.zklighter.elliot.ai/api/v1/funding-rates";

                var json = await _http.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);

                var arr = doc.RootElement
                    .GetProperty("funding_rates")
                    .EnumerateArray();

                // Tìm entry symbol == "BTC"
                double rate = 0;

                foreach (var item in arr)
                {
                    if (item.GetProperty("symbol").GetString() == "BTC")
                    {
                        rate = item.GetProperty("rate").GetDouble();
                        break;
                    }
                }

                _logger.Debug($"[Lighter] REST funding = {rate:F8}");

                return new FundingRate
                {
                    Exchange = "Lighter",
                    Symbol = "BTC-PERP",
                    Rate = rate,
                    Timestamp = DateTime.UtcNow,
                    Source = "REST"
                };
            }
            catch (Exception ex)
            {
                _logger.Error("[Lighter] Exception: " + ex.Message);

                return new FundingRate
                {
                    Exchange = "Lighter",
                    Symbol = "BTC-PERP",
                    Rate = 0,
                    Timestamp = DateTime.UtcNow,
                    Source = "ERR"
                };
            }
        }
    }
}
