using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Infra;
using HieuHieuFundingArbBot.Models;
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

        // ------------------------
        // Funding rate (REST)
        // ------------------------
        public async Task<FundingRate> GetFundingRateAsync(string symbol)
        {
            try
            {
                var url = "https://mainnet.zklighter.elliot.ai/api/v1/funding-rates?symbol=BTC-PERP";

                var json = await _http.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);

                double foundRate = 0;

                if (doc.RootElement.TryGetProperty("funding_rates", out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        var sym = item.TryGetProperty("symbol", out var sp) ? sp.GetString() : null;
                        if (string.Equals(sym, "BTC", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(sym, "BTC-PERP", StringComparison.OrdinalIgnoreCase))
                        {
                            if (item.TryGetProperty("rate", out var rateProp))
                            {
                                if (rateProp.ValueKind == JsonValueKind.Number)
                                {
                                    foundRate = rateProp.GetDouble();
                                }
                                else if (rateProp.ValueKind == JsonValueKind.String)
                                {
                                    var s = rateProp.GetString();
                                    if (!string.IsNullOrWhiteSpace(s) && double.TryParse(s, out var tmp))
                                        foundRate = tmp;
                                }
                            }

                            break;
                        }
                    }
                }
                else
                {
                    _logger.Warn("[Lighter] Unexpected JSON structure for funding_rates.");
                }

                _logger.Debug($"[Lighter] REST funding = {foundRate:F8}");

                return new FundingRate
                {
                    Exchange = "Lighter",
                    Symbol = "BTC-PERP",
                    Rate = foundRate,
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

        // ------------------------
        // Place order (SIMULATED)
        // ------------------------
        // Implements IExchangeClient.PlaceOrderAsync(OrderRequest)
        public async Task<OrderResult> PlaceOrderAsync(OrderRequest req)
        {
            try
            {
                // For now we simulate order placement to avoid real funds usage.
                _logger.Info($"[Lighter-API] Simulated PlaceOrder: {req.Side.ToUpper()} {req.Symbol} sizeUsd={req.SizeUsd}");
                await Task.Delay(120);

                var oid = "SIM-LG-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var res = new OrderResult
                {
                    Success = true,
                    OrderId = oid,
                    Message = "SIMULATED"
                };

                _logger.Debug($"[Lighter-API] Simulated order id={oid}");

                return res;
            }
            catch (Exception ex)
            {
                _logger.Error("[Lighter-API] PlaceOrder error: " + ex.Message);
                return new OrderResult { Success = false, OrderId = null, Message = ex.Message };
            }
        }

        // ------------------------
        // Cancel order (SIMULATED)
        // ------------------------
        // Implements IExchangeClient.CancelOrderAsync(string)
        public async Task<OrderResult> CancelOrderAsync(string orderId)
        {
            try
            {
                _logger.Info($"[Lighter-API] Simulated CancelOrder: {orderId}");
                await Task.Delay(80);

                return new OrderResult
                {
                    Success = true,
                    OrderId = orderId,
                    Message = "CANCELLED-SIMULATED"
                };
            }
            catch (Exception ex)
            {
                _logger.Error("[Lighter-API] CancelOrder error: " + ex.Message);
                return new OrderResult { Success = false, OrderId = orderId, Message = ex.Message };
            }
        }
    }
}
