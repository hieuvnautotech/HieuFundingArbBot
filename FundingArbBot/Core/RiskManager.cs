using FundingArbBot.Infra;
namespace HieuFundingArbBot.Core
{
    public class RiskManager
    {
        private readonly SimpleLogger _logger;
        public double PositionSizeUsd { get; }

        public RiskManager(SimpleLogger logger, double positionSizeUsd)
        {
            _logger = logger;
            PositionSizeUsd = positionSizeUsd;
        }

        public bool CheckSize(double usdAmount)
        {
            // simple check: ensure requested amount <= configured size
            var ok = usdAmount <= PositionSizeUsd;
            _logger.Debug($"Risk check for {usdAmount} USD -> {(ok ? "OK" : "REJECT")}");
            return ok;
        }
    }
}