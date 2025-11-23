using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Exchanges.Hyperliquid;
using HieuFundingArbBot.Models;

namespace HieuFundingArbBot.Core
{
    public class FundingArbEngine
    {
        private readonly IExchangeClient _hl;
        private readonly IExchangeClient _lighter;
        private readonly SpreadDetector _detector;
        private readonly TradeExecutor _executor;
        private readonly SimpleLogger _logger;
        private readonly int _intervalMs;

        // REST dùng BTC-PERP
        private readonly string _symbol = "BTC-PERP";

        // HL WS funding cache
        private FundingRate? _latestHlWsFunding;

        // HL WS client
        private readonly HyperliquidWsClient _hlWs;

        public FundingArbEngine(
            IExchangeClient hl,
            IExchangeClient lighter,
            SpreadDetector detector,
            TradeExecutor executor,
            SimpleLogger logger,
            HyperliquidWsClient hlWs,
            int intervalMs = 2000)
        {
            _hl = hl;
            _lighter = lighter;
            _detector = detector;
            _executor = executor;
            _logger = logger;
            _hlWs = hlWs;
            _intervalMs = intervalMs;
        }

        public async Task RunAsync()
        {
            _logger.Info("Engine started. Press Ctrl+C to stop.");

            // -----------------------------
            // ⭐ KẾT NỐI HL WEBSOCKET
            // -----------------------------
            try
            {
                await _hlWs.ConnectAsync();
                await _hlWs.SubscribeFundingAsync("BTC");

                _hlWs.FundingEvent += (fr) =>
                {
                    _latestHlWsFunding = fr;
                    _logger.Info($"[WS] HL realtime funding = {fr.Rate:F8}");
                };
            }
            catch (Exception ex)
            {
                _logger.Warn("[WS] Failed to init WS: " + ex.Message);
            }

            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            // -----------------------------
            // ⭐ MAIN LOOP
            // -----------------------------
            while (!cts.IsCancellationRequested)
            {
    try
    {
        FundingRate hlFr;

        if (_latestHlWsFunding != null)
        {
            hlFr = _latestHlWsFunding;
            _logger.Debug($"[HL] Using WS funding = {hlFr.Rate:F8}");
        }
        else
        {
            // 🔥 HL không có REST funding → fallback = 0
            hlFr = new FundingRate
            {
                Exchange = "Hyperliquid",
                Symbol = _symbol,
                Rate = 0,
                Timestamp = DateTime.UtcNow,
                Source = "WS-NOT-READY"
            };
            _logger.Warn("[HL] WS not ready → fallback funding = 0");
        }

        // Lighter funding (REST)
        var liFr = await _lighter.GetFundingRateAsync(_symbol);
        _logger.Debug($"[Lighter] REST funding = {liFr.Rate:F8}");

        // Compute spread
        var spread = _detector.ComputeSpread(hlFr, liFr);

        if (_detector.IsCrossingThreshold(spread))
        {
            _logger.Info($"Spread {spread:F6} >= threshold → executing arb");

            await _executor.ExecuteArbAsync(
                _hl.Name,
                _lighter.Name,
                1000.0
            );
        }
        else
        {
            _logger.Debug($"Spread {spread:F6} < threshold → no action");
        }
    }
    catch (Exception ex)
    {
        _logger.Error("Engine loop error: " + ex.Message);
    }

    await Task.Delay(_intervalMs, cts.Token);
}

            _logger.Info("Engine stopped.");
        }
    }
}
