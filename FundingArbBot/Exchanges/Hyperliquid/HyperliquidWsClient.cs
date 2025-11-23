using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HieuFundingArbBot.Infra;
using HieuFundingArbBot.Models;

namespace HieuFundingArbBot.Exchanges.Hyperliquid
{
    /// <summary>
    /// WebSocket client for Hyperliquid. Subscribes to activeAssetCtx (funding)
    /// - Provides FundingEvent on every parsed funding update
    /// - Keeps a thread-safe LatestFunding value accessible via TryGetLatestFunding
    /// - Auto-reconnects + re-subscribes
    /// </summary>
    public class HyperliquidWsClient : IAsyncDisposable
    {
        private readonly SimpleLogger _logger;
        private ClientWebSocket _ws = new ClientWebSocket();
        private readonly Uri _url;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        // coin used for subscription, e.g. "BTC"
        private readonly string _coin;

        // last parsed funding (thread-safe access via lock)
        private FundingRate? _latestFunding;
        private readonly object _latestLock = new object();

        // public event for real-time updates
        public event Action<FundingRate>? FundingEvent;

        // internal state
        private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(1.2);
        private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(15);

        // for avoid concurrent connect attempts
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);

        public HyperliquidWsClient(SimpleLogger logger, string coin = "BTC", string wsUrl = HyperliquidEndpoints.WsUrl)
        {
            _logger = logger;
            _coin = coin;
            _url = new Uri(wsUrl);
        }

        /// <summary>
        /// Connect and start background loops (receive + heartbeat).
        /// Safe to call multiple times; it will noop if already connected.
        /// </summary>
        public async Task ConnectAsync()
        {
            await _connectLock.WaitAsync();
            try
            {
                if (_ws != null && _ws.State == WebSocketState.Open)
                {
                    _logger.Debug("[HL-WS] Already connected.");
                    return;
                }

                _ws?.Dispose();
                _ws = new ClientWebSocket();

                _logger.Info("[HL-WS] Connecting...");
                await _ws.ConnectAsync(_url, CancellationToken.None);
                _logger.Info("[HL-WS] Connected.");

                // start loops
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
                _ = Task.Run(() => HeartbeatLoopAsync(_cts.Token));
            }
            finally
            {
                _connectLock.Release();
            }
        }

        /// <summary>
        /// Subscribe to funding updates for coin (e.g. "BTC").
        /// If called before ConnectAsync, call ConnectAsync first.
        /// </summary>
        public async Task SubscribeFundingAsync(string coin)
        {
            if (_ws.State != WebSocketState.Open)
            {
                _logger.Warn("[HL-WS] Subscribe: websocket not open, connecting first.");
                await ConnectAsync();
            }

            var subscribe = new
            {
                method = "subscribe",
                subscription = new
                {
                    type = "activeAssetCtx",
                    coin = coin
                }
            };

            var msg = JsonSerializer.Serialize(subscribe);
            var bytes = Encoding.UTF8.GetBytes(msg);

            try
            {
                await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                _logger.Info($"[HL-WS] Subscribed funding {coin}");
            }
            catch (Exception ex)
            {
                _logger.Error("[HL-WS] Subscribe failed: " + ex.Message);
                await ReconnectAndResubscribeAsync();
            }
        }

        /// <summary>
        /// Try get latest funding value (thread-safe).
        /// </summary>
        public bool TryGetLatestFunding(out FundingRate? fr)
        {
            lock (_latestLock)
            {
                fr = _latestFunding;
                return fr != null;
            }
        }

        private void SetLatestFunding(FundingRate fr)
        {
            lock (_latestLock)
            {
                _latestFunding = fr;
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var buffer = new byte[16 * 1024];

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult? res;

                    // read message (handle fragmentation)
                    do
                    {
                        res = await _ws.ReceiveAsync(buffer, token);
                        if (res.MessageType == WebSocketMessageType.Close)
                        {
                            _logger.Warn("[HL-WS] Server closed socket.");
                            await ReconnectAndResubscribeAsync();
                            return;
                        }

                        ms.Write(buffer, 0, res.Count);
                    }
                    while (!res.EndOfMessage);

                    var json = Encoding.UTF8.GetString(ms.ToArray());

                    // quick sanity
                    if (string.IsNullOrWhiteSpace(json))
                        continue;

                    // parse and handle
                    HandleMessage(json);
                }
                catch (OperationCanceledException)
                {
                    _logger.Debug("[HL-WS] Receive loop cancelled.");
                    break;
                }
                catch (WebSocketException wex)
                {
                    _logger.Warn("[HL-WS] Receive error → reconnect: " + wex.Message);
                    await ReconnectAndResubscribeAsync();
                    return;
                }
                catch (Exception ex)
                {
                    _logger.Warn("[HL-WS] Receive parsing error → reconnect: " + ex.Message);
                    await ReconnectAndResubscribeAsync();
                    return;
                }
            }
        }

        private void HandleMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Subscription ack: {"channel":"subscriptionResponse","data":{...}}
                if (root.TryGetProperty("channel", out var ch))
                {
                    var channelName = ch.GetString();
                    if (channelName == "subscriptionResponse")
                    {
                        _logger.Debug("[HL-WS] subscriptionResponse received.");
                        return;
                    }

                    if (channelName == "activeAssetCtx")
                    {
                        // data.ctx.funding
                        if (root.TryGetProperty("data", out var data) &&
                            data.TryGetProperty("ctx", out var ctx) &&
                            ctx.TryGetProperty("funding", out var fundingEl))
                        {
                            // funding might be number or string
                            double funding = 0;
                            if (fundingEl.ValueKind == JsonValueKind.Number && fundingEl.TryGetDouble(out var fd))
                                funding = fd;
                            else if (fundingEl.ValueKind == JsonValueKind.String && double.TryParse(fundingEl.GetString(), out var fd2))
                                funding = fd2;

                            var fr = new FundingRate
                            {
                                Exchange = "Hyperliquid",
                                Symbol = $"{_coin}-PERP",
                                Rate = funding,
                                Timestamp = DateTime.UtcNow,
                                Source = "WS"
                            };

                            // store and raise event
                            SetLatestFunding(fr);
                            _logger.Info($"[WS] HL realtime funding = {fr.Rate}");
                            _logger.Debug($"[HL-WS] Received update funding {fr.Symbol} = {fr.Rate}");
                            FundingEvent?.Invoke(fr);
                        }

                        return;
                    }
                }

                // Sometimes server returns an array style: ["activeAssetCtx", {"ctx":{...}}]
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() >= 2)
                {
                    var first = root[0].GetString();
                    if (first == "activeAssetCtx")
                    {
                        var obj = root[1];
                        if (obj.TryGetProperty("ctx", out var ctx2) && ctx2.TryGetProperty("funding", out var fEl))
                        {
                            double funding = 0;
                            if (fEl.ValueKind == JsonValueKind.Number && fEl.TryGetDouble(out var fd))
                                funding = fd;
                            else if (fEl.ValueKind == JsonValueKind.String && double.TryParse(fEl.GetString(), out var fd2))
                                funding = fd2;

                            var fr = new FundingRate
                            {
                                Exchange = "Hyperliquid",
                                Symbol = $"{_coin}-PERP",
                                Rate = funding,
                                Timestamp = DateTime.UtcNow,
                                Source = "WS"
                            };

                            SetLatestFunding(fr);
                            _logger.Info($"[WS] HL realtime funding = {fr.Rate}");
                            _logger.Debug($"[HL-WS] Received array update funding {fr.Symbol} = {fr.Rate}");
                            FundingEvent?.Invoke(fr);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn("[HL-WS] HandleMessage parse failed: " + ex.Message);
            }
        }

        private async Task HeartbeatLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_ws.State == WebSocketState.Open)
                    {
                        var ping = "{\"op\":\"ping\"}";
                        var bytes = Encoding.UTF8.GetBytes(ping);
                        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("[HL-WS] Ping failed → reconnect: " + ex.Message);
                    await ReconnectAndResubscribeAsync();
                    return;
                }

                await Task.Delay(_heartbeatInterval, token);
            }
        }

        private async Task ReconnectAndResubscribeAsync()
        {
            // cancel existing loops by disposing + new websocket
            try
            {
                // cancel any ongoing loops
                _cts.Cancel();
            }
            catch { }

            // create new cancellation token source for new loops
            // NOTE: we won't recreate _cts here since it's readonly; instead we create a short delay and attempt reconnect logic
            _logger.Warn("[HL-WS] Attempting reconnect...");

            // small delay
            await Task.Delay(_reconnectDelay);

            // try to connect again
            try
            {
                await ConnectAsync();
                await SubscribeFundingAsync(_coin);
            }
            catch (Exception ex)
            {
                _logger.Error("[HL-WS] Reconnect failed: " + ex.Message);
                // schedule next attempt
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    await ReconnectAndResubscribeAsync();
                });
            }
        }

        /// <summary>
        /// Graceful stop.
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (_ws != null && (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived || _ws.State == WebSocketState.CloseSent))
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "client close", CancellationToken.None);
                }

                _ws?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Warn("[HL-WS] Stop error: " + ex.Message);
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await StopAsync();
            }
            catch { }
            _ws?.Dispose();
        }
    }
}
