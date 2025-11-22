using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Infra;
namespace FundingArbBot.Exchanges.Hyperliquid
{
    public class HyperliquidWsClient
    {
        private readonly SimpleLogger _logger;
        private ClientWebSocket _ws = new ClientWebSocket();
        private readonly Uri _url = new Uri(HyperliquidEndpoints.WsUrl);

        public event Action<FundingRate>? FundingEvent;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        public HyperliquidWsClient(SimpleLogger logger)
        {
            _logger = logger;
        }

        // ----------------------------------
        // Connect
        // ----------------------------------
        public async Task ConnectAsync()
        {
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(_url, CancellationToken.None);
            _logger.Info("[HL-WS] Connected.");

            _ = Task.Run(ReceiveLoop);
            _ = Task.Run(HeartbeatLoop);
        }

        // ----------------------------------
        // Subscribe funding
        // ----------------------------------
        public async Task SubscribeFundingAsync(string symbol)
        {
            var msg = JsonSerializer.Serialize(new
            {
                op = "subscribe",
                channel = "funding",
                symbol = symbol
            });

            await _ws.SendAsync(
                Encoding.UTF8.GetBytes(msg),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );

            _logger.Info($"[HL-WS] Subscribed funding {symbol}");
        }

        // ----------------------------------
        // Receiving
        // ----------------------------------
        private async Task ReceiveLoop()
        {
            var buffer = new byte[8192];

            while (!_cts.IsCancellationRequested)
            {
                WebSocketReceiveResult? result = null;

                try
                {
                    result = await _ws.ReceiveAsync(buffer, _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.Warn("[HL-WS] Connection closed. Reconnecting...");
                        await ReconnectAsync();
                        return;
                    }

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    // TODO: map JSON → FundingRate
                    FundingEvent?.Invoke(new FundingRate
                    {
                        Symbol = "BTC-PERP",
                        Rate = 0.00015, // replace with real JSON
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch
                {
                    _logger.Error("[HL-WS] Receive error. Reconnecting...");
                    await ReconnectAsync();
                    return;
                }
            }
        }

        // ----------------------------------
        // Heartbeat
        // ----------------------------------
        private async Task HeartbeatLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (_ws.State == WebSocketState.Open)
                    {
                        var ping = Encoding.UTF8.GetBytes("{\"op\": \"ping\"}");
                        await _ws.SendAsync(ping, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                catch
                {
                    _logger.Warn("[HL-WS] ping failed -> reconnect");
                    await ReconnectAsync();
                }

                await Task.Delay(15000);
            }
        }

        // ----------------------------------
        // Reconnect
        // ----------------------------------
        private async Task ReconnectAsync()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();

            _ws.Dispose();
            _ws = new ClientWebSocket();

            _logger.Info("[HL-WS] Reconnecting...");

            await Task.Delay(1500);
            await ConnectAsync();
            await SubscribeFundingAsync("BTC-PERP");
        }
    }
}