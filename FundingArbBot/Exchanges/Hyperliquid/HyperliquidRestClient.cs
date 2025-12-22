using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HieuFundingArbBot.Infra;      // SimpleLogger
using HieuHieuFundingArbBot.Models;     // FundingRate, OrderRequest, OrderResult
using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Models;

namespace HieuFundingArbBot.Exchanges.Hyperliquid
{
    public class HyperliquidRestClient : IExchangeClient
    {
        private readonly HttpClient _http;
        private readonly SimpleLogger _logger;

        public string Name => "Hyperliquid";

        // optional credentials (not used for pure /info funding)
        public string ApiKey { get; }
        public string Secret { get; }

        public HyperliquidRestClient(string apiKey, string secret, SimpleLogger logger)
        {
            ApiKey = apiKey ?? "";
            Secret = secret ?? "";
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _http = new HttpClient
            {
                BaseAddress = new Uri(HyperliquidEndpoints.BaseRest)
            };

            if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                // add header if API requires it for future endpoints
                _http.DefaultRequestHeaders.Add("API-KEY", ApiKey);
            }
        }

        // ----------------------------
        // 1. Get Funding Rate REAL (robust)
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

                // Some responses are arrays: [ "activeAssetCtx", { ctx } ]
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() >= 2)
                {
                    var second = doc.RootElement[1];

                    if (second.TryGetProperty("ctx", out var ctx))
                    {
                        if (ctx.TryGetProperty("funding", out var fundingProp))
                        {
                            if (fundingProp.ValueKind == JsonValueKind.Number)
                            {
                                funding = fundingProp.GetDouble();
                            }
                            else if (fundingProp.ValueKind == JsonValueKind.String)
                            {
                                var s = fundingProp.GetString();
                                if (!double.TryParse(s, out funding))
                                    funding = 0;
                            }
                        }
                        else
                        {
                            _logger.Warn("[HL REST] /info.ctx has no 'funding' property");
                        }
                    }
                    else
                    {
                        _logger.Warn("[HL REST] /info missing 'ctx' object");
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    // fallback: sometimes API returns object with ctx directly
                    if (doc.RootElement.TryGetProperty("ctx", out var ctx2) &&
                        ctx2.TryGetProperty("funding", out var fundingProp2))
                    {
                        if (fundingProp2.ValueKind == JsonValueKind.Number)
                            funding = fundingProp2.GetDouble();
                        else if (fundingProp2.ValueKind == JsonValueKind.String)
                            double.TryParse(fundingProp2.GetString(), out funding);
                    }
                    else
                    {
                        _logger.Warn("[HL REST] /info unexpected object format");
                    }
                }
                else
                {
                    _logger.Warn("[HL REST] /info unexpected JSON kind: " + doc.RootElement.ValueKind);
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

        // -------------------------
        // 2. Submit Order (SIMULATED)
        // -------------------------
        // Uses the project's OrderRequest / OrderResult models (HieuHieuFundingArbBot.Models).
        // Currently simulation — safe to call for testing with tiny sizes (e.g. 1 USD).
        public async Task<OrderResult> PlaceOrderAsync(OrderRequest req)
        {
            try
            {
                // Basic validation to avoid accidental huge orders
                if (req == null) throw new ArgumentNullException(nameof(req));
                if (req.SizeUsd <= 0)
                {
                    return new OrderResult
                    {
                        Success = false,
                        OrderId = null,
                        Message = "Invalid sizeUsd",
                        Raw = null
                    };
                }

                // Log and simulate network latency
                _logger.Info($"[HL-API] (SIM) PlaceOrder: side={req.Side} symbol={req.Symbol} sizeUsd={req.SizeUsd} market={req.Market}");
                await Task.Delay(120);

                var oid = "SIM-HL-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                return new OrderResult
                {
                    Success = true,
                    OrderId = oid,
                    Message = "SIMULATED",
                    Raw = null
                };
            }
            catch (Exception ex)
            {
                _logger.Error("[HL-API] PlaceOrder error: " + ex.Message);
                return new OrderResult
                {
                    Success = false,
                    OrderId = null,
                    Message = ex.Message,
                    Raw = null
                };
            }
        }

        // -------------------------
        // 3. Cancel Order (SIMULATED)
        // -------------------------
        public async Task<OrderResult> CancelOrderAsync(string orderId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(orderId))
                {
                    return new OrderResult { Success = false, OrderId = orderId, Message = "orderId empty", Raw = null };
                }

                _logger.Info($"[HL-API] (SIM) CancelOrder: {orderId}");
                await Task.Delay(80);

                return new OrderResult
                {
                    Success = true,
                    OrderId = orderId,
                    Message = "CANCELLED-SIMULATED",
                    Raw = null
                };
            }
            catch (Exception ex)
            {
                _logger.Error("[HL-API] CancelOrder error: " + ex.Message);
                return new OrderResult { Success = false, OrderId = orderId, Message = ex.Message, Raw = null };
            }
        }

        // -------------------------
        // 4. (Optional) helper to call real order endpoint later
        // -------------------------
        // You can add a method here to build signed requests (if HL requires HMAC) and call real order endpoints.
    }
}
