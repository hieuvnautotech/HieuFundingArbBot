using System.Net.Http;
using System.Text.Json;
using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Models;
using HieuFundingArbBot.Infra;

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
                var url = "https://mainnet.zklighter.elliot.ai/api/v1/funding-rates?symbol=BTC-PERP";
                var json = await _http.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);

                var arr = doc.RootElement
                    .GetProperty("funding_rates")
                    .EnumerateArray();

                // Lighter uses "BTC" instead of "BTC-PERP"
                foreach (var item in arr)
                {
                    if (item.GetProperty("symbol").GetString() == "BTC")
                    {
                        double rate = item.GetProperty("rate").GetDouble();

                        return new FundingRate
                        {
                            Exchange = "Lighter",
                            Symbol = "BTC-PERP",
                            Rate = rate,
                            Timestamp = DateTime.UtcNow,
                            Source = "REST"
                        };
                    }
                }

                throw new Exception("BTC not found in Lighter response");
            }
            catch (Exception ex)
            {
                _logger.Error($"[Lighter] Exception: {ex.Message}");
                return new FundingRate
                {
                    Exchange = "Lighter",
                    Symbol = symbol,
                    Rate = 0,
                    Timestamp = DateTime.UtcNow,
                    Source = "ERROR"
                };
            }
        }
    }
}
