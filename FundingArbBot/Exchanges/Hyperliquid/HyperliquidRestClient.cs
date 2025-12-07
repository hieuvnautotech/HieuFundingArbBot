using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using FundingArbBot.Models;
using HieuFundingArbBot.Exchanges.Hyperliquid;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Models;


namespace FundingArbBot.Exchanges.Hyperliquid

{
    // Hyperliquid REST adapter (safer/simulated order calls)
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
                BaseAddress = new Uri(HyperliquidEndpoints.BaseRest)
            };

            // If endpoints require API-key header, add here (optional)
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                // Example header — adjust to HL API if/when required
                _http.DefaultRequestHeaders.Add("API-KEY", apiKey);
            }
        }

        // ----------------------------
        // 1) Get Funding Rate (REST)
        // ----------------------------
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

                double funding = 0;

                // Common HL response is an array: [ "activeAssetCtx", { ctx } ]
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() >= 2)
                {
                    var ctxElem = doc.RootElement[1].GetProperty("ctx");

                    if (ctxElem.TryGetProperty("funding", out var fundingProp))
                    {
                        // funding may be number or string
                        if (fundingProp.ValueKind == JsonValueKind.Number)
                        {
                            funding = fundingProp.GetDouble();
                        }
                        else if (fundingProp.ValueKind == JsonValueKind.String &&
                                 double.TryParse(fundingProp.GetString(), out var tmp))
                        {
                            funding = tmp;
                        }
                    }
                }
                else
                {
                    _logger.Warn("[HL REST] Unexpected /info response format (not array)");
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

        // ----------------------------
        // 2) Get Positions (simple)
        // ----------------------------
        public async Task<string> GetPositionsAsync()
        {
            try
            {
                var res = await _http.GetAsync(HyperliquidEndpoints.Positions);
                return await res.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.Error("[HL REST] GetPositions error: " + ex.Message);
                return string.Empty;
            }
        }

        // ----------------------------
        // 3) Submit Order (SIMULATED / safe)
        // ----------------------------
        // Note: keep this simulated until you confirm exact HL order API and OrderRequest fields.
        public async Task<OrderResult> PlaceOrderAsync(OrderRequest req)
        {
            // Defensive: avoid referencing fields that may not exist on OrderRequest in your codebase.
            try
            {
                // Build a minimal log-friendly description using the common fields.
                var side = (req?.Side ?? "unknown").ToString();
                var market = !string.IsNullOrEmpty(req?.Market) ? req.Market : (req?.Symbol ?? "unknown");
                var sizeUsd = req?.SizeUsd ?? 0.0;

                _logger.Info($"[HL-API] (SIM) PlaceOrder: side={side}, market={market}, sizeUsd={sizeUsd}");

                // simulate latency
                await Task.Delay(120);

                var oid = "SIM-HL-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                return new OrderResult
                {
                    Success = true,
                    OrderId = oid,
                    Message = "SIMULATED",
                    Raw = $"{{\"sim\":\"placed\",\"side\":\"{side}\",\"market\":\"{market}\",\"sizeUsd\":{sizeUsd}}}"
                };
            }
            catch (Exception ex)
            {
                _logger.Error("[HL-API] PlaceOrder exception: " + ex.Message);
                return new OrderResult
                {
                    Success = false,
                    OrderId = null,
                    Message = ex.Message,
                    Raw = ""
                };
            }
        }

        // ----------------------------
        // 4) Cancel Order (SIMULATED / safe)
        // ----------------------------
        public async Task<OrderResult> CancelOrderAsync(string orderId)
        {
            try
            {
                _logger.Info($"[HL-API] (SIM) CancelOrder: {orderId}");
                await Task.Delay(80);

                return new OrderResult
                {
                    Success = true,
                    OrderId = orderId,
                    Message = "CANCELLED-SIMULATED",
                    Raw = $"{{\"sim\":\"cancelled\",\"orderId\":\"{orderId}\"}}"
                };
            }
            catch (Exception ex)
            {
                _logger.Error("[HL-API] CancelOrder exception: " + ex.Message);
                return new OrderResult
                {
                    Success = false,
                    OrderId = orderId,
                    Message = ex.Message,
                    Raw = ""
                };
            }
        }
    }
}
