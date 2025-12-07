using System;

namespace FundingArbBot.Models
{
    // Minimal order request used by the clients/engine.
    // Thêm/điều chỉnh trường nếu sau này muốn gửi request thực tới sàn.
    public class OrderRequest
    {
        // "buy" or "sell" (or "long"/"short" depending code)
        public string? Side { get; set; }

        // Symbol like "BTC-PERP"
        public string? Symbol { get; set; }

        // Market or exchange-specific market id (optional)
        public string? Market { get; set; }

        // Size in USD (we use USD sizing in executor)
        public double? SizeUsd { get; set; }

        // Optional client-provided id for idempotency
        public string? ClientOrderId { get; set; }

        // Optional order type e.g. "market" or "limit"
        public string? OrderType { get; set; }

        // Optional price (for limit orders)
        public double? Price { get; set; }
    }

    // Standardized result returned by PlaceOrderAsync / CancelOrderAsync
    
}
