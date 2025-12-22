using System;
using System.Linq;
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
using HieuFundingArbBot.Models;

class Program
{
    static async Task Main(string[] args)
    {
        // ------------------------------
        // TEST API MODE (unchanged)
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

        // Read HL private key from environment (if any)
        var hlPrivateKey = Environment.GetEnvironmentVariable("HL_BOT_PRIVATE_KEY");
        if (string.IsNullOrWhiteSpace(hlPrivateKey))
        {
            logger.Warn("HL_BOT_PRIVATE_KEY not set. Running read-only / simulated orders (no wallet signing).");
        }
        else
        {
            logger.Info("HL private key loaded from environment (not printed).");
        }

        // Optional env to force using real Hyperliquid REST client:
        // set USE_REAL_HL=1 to use HyperliquidRestClient (requires its constructor supports keys).
        var useRealHl = Environment.GetEnvironmentVariable("USE_REAL_HL") == "1";

        IExchangeClient hl;
        if (useRealHl)
        {
            // NOTE:
            // current HyperliquidRestClient constructor in your project expects:
            //   HyperliquidRestClient(string apiKey, string secret, SimpleLogger logger)
            // so we call that. If you later extend HyperliquidRestClient to accept a privateKey
            // change the constructor there and update this call accordingly.
            string apiKey = "";    // optionally load from config or env
            string secret = "";    // optionally load from config or env

            hl = new HyperliquidRestClient(apiKey, secret, logger);
            logger.Info("Using HyperliquidRestClient (REAL) as HL client. Note: HL private key (if set) is NOT used unless HyperliquidRestClient is updated to accept it.");
        }
        else
        {
            // Use wrapper / simulated HL client (works now)
            hl = new HyperliquidClient("Hyperliquid", logger);
            logger.Info("Using HyperliquidClient (SIMULATED) as HL client.");
        }

        // Lighter real client (we implemented REST reader)
        IExchangeClient lighter = new LighterRealClient(logger);

        var spreadDetector = new SpreadDetector(logger, config.BotConfig.SpreadThreshold);
        var riskManager = new RiskManager(logger, config.BotConfig.PositionSizeUsd);
        var hlWs = new HyperliquidWsClient(logger);
        var tel = new TelegramNotifier(config.TelegramConfig, logger);
        var executor = new TradeExecutor(logger, tel, riskManager);

        // If user wants a one-shot test to place 1 USD size, run and exit.
        if (args.Contains("place-1"))
        {
            logger.Info("One-shot test: executing arb with size = 1 USD (test mode).");

            try
            {
                // ExecuteArbAsync expects exchange names; use the runtime client names
                await executor.ExecuteArbAsync(hl.Name, lighter.Name, 1.0);
                logger.Info("One-shot test done. Exiting.");
            }
            catch (Exception ex)
            {
                logger.Error("One-shot execution failed: " + ex.Message);
            }

            return;
        }

        var engine = new FundingArbEngine(
            hl, lighter, spreadDetector, executor, logger, hlWs,
            config.BotConfig.CheckIntervalMs);

        if (args.Contains("hl-real-1"))
        {
            var hlReal = new HyperliquidRealClient(logger);

            await hlReal.PlaceOrderAsync(new OrderRequest
            {
                Side = "buy",
                Symbol = "BTC-PERP",
                SizeUsd = 1
            });

            logger.Warn("🔥 REAL ORDER SENT — CHECK HYPERLIQUID UI");
            return;
        }




        await engine.RunAsync();
    }

    // ------------------------------
    // TEST HL API (unchanged)
    // ------------------------------
    static async Task TestHyperliquidApi()
    {
        Console.WriteLine("🔍 Testing Hyperliquid API /info (meta)...");

        using var http = new HttpClient();
        http.BaseAddress = new Uri("https://api.hyperliquid.xyz");

        var body = new { type = "meta" };
        var json = JsonSerializer.Serialize(body);

        var resp = await http.PostAsync("/info",
            new StringContent(json, Encoding.UTF8, "application/json"));

        var text = await resp.Content.ReadAsStringAsync();

        Console.WriteLine("STATUS = " + resp.StatusCode);
        Console.WriteLine("BODY = " + text);
    }
}
