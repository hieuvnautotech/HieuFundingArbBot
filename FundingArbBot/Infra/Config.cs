using System.Text.Json;

namespace HieuFundingArbBot.Infra
{
    public class BotConfig
    {
        public string Mode { get; set; } = "FundingOnly";
        public double SpreadThreshold { get; set; } = 0.0001;
        public int CheckIntervalMs { get; set; } = 2000;
        public double PositionSizeUsd { get; set; } = 1000.0;
    }

    public class TelegramConfig
    {
        public bool Enabled { get; set; }
        public string BotToken { get; set; } = "";
        public string ChatId { get; set; } = "";
    }

    // ⭐ NEW: API Config for Hyperliquid + others later
    public class ApiConfig
    {
        public string HyperliquidApiKey { get; set; } = "";
        public string HyperliquidSecret { get; set; } = "";
    }

    public class AppConfig
    {
        public BotConfig BotConfig { get; set; } = new BotConfig();
        public TelegramConfig TelegramConfig { get; set; } = new TelegramConfig();

        // ⭐ MUST HAVE: ApiConfig property
        public ApiConfig ApiConfig { get; set; } = new ApiConfig();

        public static AppConfig LoadFromFile(string path)
        {
            var s = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(s,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
    }

    public static class Config
    {
        public static AppConfig Load(string path)
        {
            return AppConfig.LoadFromFile(path);
        }
    }
}
