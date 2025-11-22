using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Models;
using HieuFundingArbBot.Infra;
namespace HieuFundingArbBot.Exchanges
{
    public class HyperliquidClient : IExchangeClient
    {
        private readonly SimpleLogger _logger;
        public string Name { get; }

        private readonly Random _rnd = new Random();

        public HyperliquidClient(string name, SimpleLogger logger)
        {
            Name = name;
            _logger = logger;
        }

        public Task<FundingRate> GetFundingRateAsync(string symbol)
        {
            // simulate funding rate around +0.0002 (0.02%) with jitter
            var rate = 0.0002 + (_rnd.NextDouble() - 0.5) * 0.00005;
            var fr = new FundingRate { Symbol = symbol, Rate = rate, Timestamp = DateTime.UtcNow };
            _logger.Info($"{Name} simulated FR {symbol} = {fr.Rate:F6}");
            return Task.FromResult(fr);
        }
    }
}