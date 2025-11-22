using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Models;
using Infra;
namespace FundingArbBot.Exchanges.Hyperliquid
{
    public class HyperliquidRestClient
    {
        private readonly HttpClient _http;
        private readonly SimpleLogger _logger;

        public string ApiKey { get; }
        public string Secret { get; }

        public HyperliquidRestClient(string apiKey, string secret, SimpleLogger logger)
        {
            ApiKey = apiKey;
            Secret = secret;
            _logger = logger;

            _http = new HttpClient
            {
                BaseAddress = new Uri(HyperliquidEndpoints.BaseRest)
            };

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _http.DefaultRequestHeaders.Add("API-KEY", ApiKey);
            }
        }

        // ----------------------------
        // 1. Get Funding Rate REAL
        // ----------------------------
        public async Task<FundingRate?> GetFundingAsync(string symbol)
        {
            try
            {
                var url = $"{HyperliquidEndpoints.Funding}?symbol={symbol}";
                var res = await _http.GetAsync(url);
                var body = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    _logger.Error($"HL Funding Error: {body}");
                    return null;
                }

                // TODO: map JSON → FundingRate
                var fr = new FundingRate
                {
                    Symbol = symbol,
                    Rate = 0.00012, // placeholder, replace with JSON parsed value
                    Timestamp = DateTime.UtcNow
                };

                return fr;
            }
            catch (Exception ex)
            {
                _logger.Error("HL Funding exception: " + ex.Message);
                return null;
            }
        }

        // ----------------------------
        // 2. Get Positions
        // ----------------------------
        public async Task<string> GetPositionsAsync()
        {
            var res = await _http.GetAsync(HyperliquidEndpoints.Positions);
            return await res.Content.ReadAsStringAsync();
        }

        // ----------------------------
        // 3. Submit Order
        // ----------------------------
        public async Task<string> SubmitOrderAsync(object order)
        {
            var json = JsonSerializer.Serialize(order);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var res = await _http.PostAsync(HyperliquidEndpoints.Order, content);
            return await res.Content.ReadAsStringAsync();
        }

        // ----------------------------
        // 4. Cancel Order
        // ----------------------------
        public async Task<string> CancelOrderAsync(string orderId)
        {
            var content = new StringContent($"{{\"orderId\":\"{orderId}\"}}",
                System.Text.Encoding.UTF8, "application/json");

            var res = await _http.PostAsync(HyperliquidEndpoints.Order + "/cancel", content);
            return await res.Content.ReadAsStringAsync();
        }
    }
}