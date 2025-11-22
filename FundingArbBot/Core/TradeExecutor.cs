using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Infra;
namespace HieuFundingArbBot.Core
{
    // Simulated executor — only logs; later integrate real order placement
    public class TradeExecutor
    {
        private readonly SimpleLogger _logger;
        private readonly TelegramNotifier _telegram;
        private readonly RiskManager _risk;

        public TradeExecutor(SimpleLogger logger, TelegramNotifier telegram, RiskManager risk)
        {
            _logger = logger;
            _telegram = telegram;
            _risk = risk;
        }

        public async Task<bool> ExecuteArbAsync(string shortExchange, string longExchange, double sizeUsd)
        {
            _logger.Info($"[EXECUTOR] Attempting arb: SHORT {shortExchange} / LONG {longExchange} - size {sizeUsd} USD");

            if (!_risk.CheckSize(sizeUsd))
            {
                _logger.Warn("Trade rejected by RiskManager.");
                return false;
            }

            // simulate order placement
            _logger.Info($"Placing SHORT on {shortExchange} ... (SIMULATED)");
            _logger.Info($"Placing LONG on {longExchange} ... (SIMULATED)");

            await _telegram.SendMessageAsync($"Executed simulated arb: SHORT {shortExchange}, LONG {longExchange}, size {sizeUsd}");

            // simulate success
            _logger.Info("Simulated orders filled.");
            return true;
        }
    }
}