using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.Signer;
using Nethereum.Util;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Interfaces;
using HieuHieuFundingArbBot.Models;
using HieuFundingArbBot.Models;

namespace HieuFundingArbBot.Exchanges.Hyperliquid
{
    // ⚠️ REAL MONEY CLIENT – size is HARD-LIMITED to <= 1 USD
    public class HyperliquidRealClient : IExchangeClient
    {
        private readonly HttpClient _http;
        private readonly SimpleLogger _logger;
        private readonly EthECKey _key;
        private readonly string _address;

        public string Name => "Hyperliquid-REAL";

        public HyperliquidRealClient(SimpleLogger logger)
        {
            _logger = logger;

            var pk = Environment.GetEnvironmentVariable("HL_BOT_PRIVATE_KEY");
            if (string.IsNullOrWhiteSpace(pk))
                throw new Exception("HL_BOT_PRIVATE_KEY chưa được set");

            _key = new EthECKey(pk);
            _address = _key.GetPublicAddress();

            _http = new HttpClient
            {
                BaseAddress = new Uri("https://api.hyperliquid.xyz")
            };

            _logger.Warn($"[HL-REAL] USING REAL WALLET: {_address}");
        }

        // ========= FUNDING (reuse REST info) =========
        public async Task<FundingRate> GetFundingRateAsync(string symbol)
        {
            var body = new { type = "activeAssetCtx", coin = "BTC" };
            var res = await _http.PostAsync("/info",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            double funding = 0;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var ctx = doc.RootElement[1].GetProperty("ctx");
                funding = ctx.GetProperty("funding").GetDouble();
            }

            return new FundingRate
            {
                Exchange = Name,
                Symbol = symbol,
                Rate = funding,
                Timestamp = DateTime.UtcNow,
                Source = "REST"
            };
        }

        // ========= REAL ORDER =========
        public async Task<OrderResult> PlaceOrderAsync(OrderRequest req)
        {
            if (req.SizeUsd > 1.0)
                throw new Exception("🔥 REAL MODE chỉ cho phép <= 1 USD");

            long nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var action = new
            {
                type = "order",
                orders = new[]
                {
                    new {
                        coin = "BTC",
                        is_buy = req.Side.ToLower() == "buy",
                        sz = Math.Round(req.SizeUsd.Value / 50000, 6), // ~ BTC size
                        limit_px = (double?)null,
                        order_type = "market",
                        reduce_only = false
                    }
                }
            };

            var payload = new
            {
                action,
                nonce,
                address = _address
            };

            var payloadJson = JsonSerializer.Serialize(payload);
            var hash = Sha3Keccack.Current.CalculateHash(payloadJson);
            var sig = _key.SignAndCalculateV(hash);

            var request = new
            {
                action,
                nonce,
                signature = "0x" + sig,
                address = _address
            };

            _logger.Warn($"[HL-REAL] SENDING REAL ORDER {req.Side} {req.SizeUsd} USD");

            var res = await _http.PostAsync("/exchange",
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

            var text = await res.Content.ReadAsStringAsync();

            _logger.Warn($"[HL-REAL] RESPONSE = {text}");

            return new OrderResult
            {
                Success = res.IsSuccessStatusCode,
                OrderId = nonce.ToString(),
                Message = text,
                Raw = text
            };
        }

        public Task<OrderResult> CancelOrderAsync(string orderId)
        {
            throw new NotSupportedException("Cancel chưa implement");
        }
    }
}
