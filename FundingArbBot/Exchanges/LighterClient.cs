using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Models;
using HieuFundingArbBot.Infra;
namespace HieuFundingArbBot.Exchanges
{
    // MOCK client - simulate lower funding rate
    public class LighterClient : IExchangeClient
    {
        private readonly SimpleLogger _logger;
        public string Name { get; }
        private readonly Random _rnd = new Random();

        public LighterClient(string name, SimpleLogger logger)
        {
            Name = name;
            _logger = logger;
        }

        public Task<FundingRate> GetFundingRateAsync(string symbol)
        {
            // simulate funding rate around 0.0 (or smaller) with jitter
            var rate = 0.0 + (_rnd.NextDouble() - 0.5) * 0.00005;
            var fr = new FundingRate { Symbol = symbol, Rate = rate, Timestamp = DateTime.UtcNow };
            _logger.Info($"{Name} simulated FR {symbol} = {fr.Rate:F6}");
            return Task.FromResult(fr);
        }
    }
}