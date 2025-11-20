using HieuFundingArbBot.Interfaces;
using FundingArbBot.Infra;
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
        private readonly string _symbol = "BTC-PERP";

        public FundingArbEngine(
            IExchangeClient hl,
            IExchangeClient lighter,
            SpreadDetector detector,
            TradeExecutor executor,
            SimpleLogger logger,
            int intervalMs = 2000)
        {
            _hl = hl;
            _lighter = lighter;
            _detector = detector;
            _executor = executor;
            _logger = logger;
            _intervalMs = intervalMs;
        }

        public async Task RunAsync()
        {
            _logger.Info("Engine started. Press Ctrl+C to stop.");
            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var hlFr = await _hl.GetFundingRateAsync(_symbol);
                    var liFr = await _lighter.GetFundingRateAsync(_symbol);

                    var spread = _detector.ComputeSpread(hlFr, liFr);

                    if (_detector.IsCrossingThreshold(spread))
                    {
                        _logger.Info($"Spread {spread:F6} >= threshold -> executing arb.");
                        // We short HL, long Lighter (as decided)
                        await _executor.ExecuteArbAsync(_hl.Name, _lighter.Name, 1000.0);
                    }
                    else
                    {
                        _logger.Debug($"Spread {spread:F6} < threshold -> no action.");
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