using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Exchanges;
using HieuFundingArbBot.Core;
using HieuFundingArbBot.Interfaces;
using HieuFundingArbBot.Exchanges.Hyperliquid;
using HieuFundingArbBot.Exchanges.Lighter;
using HieuFundingArbBot.Exchanges.Hyperliquid;

class Program
{
    static async Task Main(string[] args)
    {
        // ------------------------------
        // TEST API MODE
        // ------------------------------
        if (args.Length > 0 && args[0] == "test-hl")
        {
            await TestHyperliquidApi();
            return;
        }

        // NORMAL BOT MODE
        Console.WriteLine("Funding Arbitrage Bot — Booting...");
        var config = Config.Load("appsettings.json");
        var logger = new SimpleLogger();

        var tel = new TelegramNotifier(config.TelegramConfig, logger);

        // Tạm thời dùng wrapper HyperliquidClient (simulated/adapter).
// Sau này khi HyperliquidRestClient đầy đủ, đổi lại.
IExchangeClient hl = new HyperliquidClient("Hyperliquid", logger);
        // Lighter real client (we implemented simulated Place/Cancel there).
IExchangeClient lighter = new LighterRealClient(logger);

        var spreadDetector = new SpreadDetector(logger, config.BotConfig.SpreadThreshold);
        var riskManager = new RiskManager(logger, config.BotConfig.PositionSizeUsd);
        var hlWs = new HyperliquidWsClient(logger);

        var executor = new TradeExecutor(logger, tel, riskManager);

        var engine = new FundingArbEngine(
            hl, lighter, spreadDetector, executor, logger, hlWs,
            config.BotConfig.CheckIntervalMs);

        await engine.RunAsync();
    }

    // ------------------------------
    // TEST HL API
    // ------------------------------
    static async Task TestHyperliquidApi()
    {
    Console.WriteLine("🔍 Testing Hyperliquid API /info (meta)...");

    using var http = new HttpClient();
    http.BaseAddress = new Uri("https://api.hyperliquid.xyz");

    var body = new
    {
        type = "meta"
    };

    var json = JsonSerializer.Serialize(body);

    var resp = await http.PostAsync("/info",
        new StringContent(json, Encoding.UTF8, "application/json"));

    var text = await resp.Content.ReadAsStringAsync();

    Console.WriteLine("STATUS = " + resp.StatusCode);
    Console.WriteLine("BODY = " + text);
}
}
