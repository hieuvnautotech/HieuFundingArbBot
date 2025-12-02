using System.Net.Http;
using System.Text;
using System.Text.Json;
using FundingArbBot.Models;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Models;

namespace HieuFundingArbBot.Exchanges.Hyperliquid
{
    // NOTE: This implementation currently provides a simulated Place/Cancel.
    // When ready to use real order API, replace PlaceOrderAsync/CancelOrderAsync
    // with actual signed HTTP calls to Hyperliquid order endpoints.
    public class HyperliquidRestClient : HieuFundingArbBot.Interfaces.IExchangeClient
    {
        private readonly HttpClient _http;
        private readonly SimpleLogger _logger;

        public string Name => "Hyperliquid";

        public HyperliquidRestClient(string apiKey, string secret, SimpleLogger logger)
        {
            _logger = logger;
            _http = new HttpClient { BaseAddress = new Uri(HyperliquidEndpoints.BaseRest) };
            // If API key required for endpoints, add default headers here.
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

                // Response format: [ "activeAssetCtx", { ctx } ]
                // Some responses might be different; guard carefully.
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() >= 2)
                {
                    var ctx = doc.RootElement[1].GetProperty("ctx");
                    double funding = 0;
                    if (ctx.TryGetProperty("funding", out var fundingProp))
                    {
                        // funding may be number or string — handle both
                        if (fundingProp.ValueKind == JsonValueKind.Number)
                            funding = fundingProp.GetDouble();
                        else if (fundingProp.ValueKind == JsonValueKind.String && double.TryParse(fundingProp.GetString(), out var tmp))
                            funding = tmp;
                    }

                    return new FundingRate
                    {
                        Exchange = "Hyperliquid",
                        Symbol = symbol,
                        Rate = funding,
                        Timestamp = DateTime.UtcNow,
                        Source = "REST"
                    };
                }

                _logger.Warn("[HL REST] Unexpected /info response format");
                return new FundingRate
                {
                    Exchange = "Hyperliquid",
                    Symbol = symbol,
                    Rate = 0,
                    Timestamp = DateTime.UtcNow,
                    Source = "REST"
                };
            }
            catch (Exception ex)
            {
                _logger.Error("[HL REST] Error: " + ex.Message);
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

        // -------------------------
        // PlaceOrder (SIMULATED)
        // -------------------------
        public async Task<OrderResult> PlaceOrderAsync(OrderRequest req)
        {
            // Simulate a place order (no real funds risk).
            _logger.Info($"[HL-API] Simulated PlaceOrder: {req.Side.ToUpper()} {req.Symbol} sizeUsd={req.SizeUsd} market={req.Market}");
            await Task.Delay(120); // simulate network latency

            var oid = "SIM-HL-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return new OrderResult { Success = true, OrderId = oid, Message = "SIMULATED" };
        }

        // -------------------------
        // CancelOrder (SIMULATED)
        // -------------------------
        public async Task<OrderResult> CancelOrderAsync(string orderId)
        {
            _logger.Info($"[HL-API] Simulated CancelOrder: {orderId}");
            await Task.Delay(80);
            return new OrderResult { Success = true, OrderId = orderId, Message = "CANCELLED-SIMULATED" };
        }
    }
}
