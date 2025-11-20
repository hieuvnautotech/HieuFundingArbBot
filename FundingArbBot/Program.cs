using System;
using System.Threading.Tasks;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Exchanges;
using HieuFundingArbBot.Core;
using HieuFundingArbBot.Interfaces;
using FundingArbBot.Infra;
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Funding Arbitrage Bot — Booting...");
        var config = Config.Load("appsettings.json");
        var logger = new SimpleLogger();

        var tel = new TelegramNotifier(config.TelegramConfig, logger);

        // Exchange clients (mocked for now - later replace with real HTTP/WS clients)
        IExchangeClient hl = new HyperliquidClient("Hyperliquid", logger);
        IExchangeClient lighter = new LighterClient("Lighter", logger);

        var spreadDetector = new SpreadDetector(logger, config.BotConfig.SpreadThreshold);
        var riskManager = new RiskManager(logger, config.BotConfig.PositionSizeUsd);
        var executor = new TradeExecutor(logger, tel, riskManager);

        var engine = new FundingArbEngine(
            hl, lighter, spreadDetector, executor, logger, config.BotConfig.CheckIntervalMs);

        await engine.RunAsync(); // runs until cancelled (Ctrl+C)
    }
}
