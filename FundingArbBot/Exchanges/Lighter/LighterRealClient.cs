using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Models;
using FundingArbBot.Models;

namespace HieuFundingArbBot.Exchanges.Lighter
{
    public class LighterRealClient : IExchangeClient
    {
        private readonly HttpClient _http = new HttpClient();
        private readonly SimpleLogger _logger;

        public string Name => "Lighter";

        public LighterRealClient(SimpleLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _http.Timeout = TimeSpan.FromSeconds(10);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("FundingArbBot/1.0");
        }

        public async Task<FundingRate> GetFundingRateAsync(string symbol)
        {
            string token = NormalizeSymbolToToken(symbol);

            try
            {
                var url = "https://mainnet.zklighter.elliot.ai/api/v1/funding-rates";
                var json = await _http.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("funding_rates", out var frArr))
                {
                    _logger.Warn("[Lighter] Response missing 'funding_rates' field");
                    return FallbackFunding(symbol);
                }

                double rate = 0.0;
                bool found = false;

                foreach (var item in frArr.EnumerateArray())
                {
                    var itemSym = item.GetProperty("symbol").GetString() ?? "";
                    if (SymbolsMatch(itemSym, token))
                    {
                        rate = item.GetProperty("rate").GetDouble();
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _logger.Warn($"[Lighter] funding for {token} not found");
                    return FallbackFunding(symbol);
                }

                _logger.Debug($"[Lighter] REST funding = {rate:F8}");

                return new FundingRate
                {
                    Exchange = "Lighter",
                    Symbol = symbol,
                    Rate = rate,
                    Timestamp = DateTime.UtcNow,
                    Source = "REST"
                };
            }
            catch (Exception ex)
            {
                _logger.Error("[Lighter] Exception: " + ex.Message);
                return FallbackFunding(symbol);
            }
        }

        // -----------------------
        // Orders (simulated)
        // -----------------------
        public async Task<OrderResult> PlaceOrderAsync(OrderRequest req)
        {
            _logger.Info($"[Lighter] Simulating PlaceOrder {req.Side} {req.Symbol} sizeUsd={req.SizeUsd}");
            await Task.Delay(200);

            // Note: req.Price may be nullable or not in your OrderRequest;
            // if it's nullable, use req.Price ?? 0.0; if non-nullable, use req.Price.
            double avgPrice = 0.0;
            try { avgPrice = req.Price; } catch { /* fallback 0.0 */ }

            return new OrderResult
            {
                OrderId = Guid.NewGuid().ToString(),
                Status = "filled",
                FilledSizeUsd = req.SizeUsd,
                AvgPrice = avgPrice,
                Raw = "{\"sim\":\"lighter_ok\"}"
            };
        }

        public async Task<OrderResult> CancelOrderAsync(string orderId)
        {
            _logger.Info($"[Lighter] Simulating CancelOrder {orderId}");
            await Task.Delay(100);

            return new OrderResult
            {
                OrderId = orderId,
                Status = "cancelled",
                FilledSizeUsd = 0.0,
                AvgPrice = 0.0,
                Raw = "{\"sim_cancel\":\"ok\"}"
            };
        }

        // -----------------------
        // Helpers
        // -----------------------
        private FundingRate FallbackFunding(string symbol)
        {
            return new FundingRate
            {
                Exchange = "Lighter",
                Symbol = symbol,
                Rate = 0,
                Timestamp = DateTime.UtcNow,
                Source = "ERR"
            };
        }

        private static string NormalizeSymbolToToken(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return "BTC";
            var parts = symbol.Split('-', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0].ToUpperInvariant() : symbol.ToUpperInvariant();
        }

        private static bool SymbolsMatch(string itemSymbol, string token)
        {
            if (string.IsNullOrEmpty(itemSymbol) || string.IsNullOrEmpty(token)) return false;
            if (itemSymbol.Equals(token, StringComparison.OrdinalIgnoreCase)) return true;
            if (itemSymbol.StartsWith(token + "-", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
