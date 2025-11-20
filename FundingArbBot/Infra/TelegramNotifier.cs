using FundingArbBot.Infra;
namespace HieuFundingArbBot.Infra
{
    public class TelegramNotifier
    {
        private readonly TelegramConfig _cfg;
        private readonly SimpleLogger _logger;

        public TelegramNotifier(TelegramConfig cfg, SimpleLogger logger)
        {
            _cfg = cfg;
            _logger = logger;
        }

        public Task SendMessageAsync(string text)
        {
            if (!_cfg.Enabled)
            {
                _logger.Debug("Telegram disabled - message not sent: " + text);
                return Task.CompletedTask;
            }

            // TODO: implement real Telegram API calls (HttpClient)
            _logger.Info("Telegram send (SIMULATED): " + text);
            return Task.CompletedTask;
        }
    }
}