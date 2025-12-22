using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Globalization;
using HieuFundingArbBot.Infra;      // SimpleLogger
using HieuHieuFundingArbBot.Models;     // FundingRate

namespace HieuFundingArbBot.Exchanges.Hyperliquid
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

        public async Task ConnectAsync()
        {
            _ws = new ClientWebSocket();
            _logger.Info("[HL-WS] Connecting...");
            await _ws.ConnectAsync(_url, CancellationToken.None);
            _logger.Info("[HL-WS] Connected.");

            // start loops
            _ = Task.Run(ReceiveLoop);
            _ = Task.Run(HeartbeatLoop);
        }

        public async Task SubscribeFundingAsync(string coin)
        {
            // Use "method"/"subscription" style (matches REST WS examples used earlier)
            var msg = JsonSerializer.Serialize(new
            {
                method = "subscribe",
                subscription = new
                {
                    type = "activeAssetCtx",
                    coin = coin
                }
            });

            var buf = Encoding.UTF8.GetBytes(msg);
            await _ws.SendAsync(new ArraySegment<byte>(buf), WebSocketMessageType.Text, true, CancellationToken.None);

            _logger.Info($"[HL-WS] Subscribed funding {coin}");
        }

        private async Task ReceiveLoop()
        {
            // We'll accumulate when message is fragmented
            var buffer = new byte[4096];

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var ms = new System.IO.MemoryStream();

                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _logger.Warn("[HL-WS] Closed → Reconnecting");
                            await ReconnectAsync();
                            return;
                        }

                        ms.Write(buffer, 0, result.Count);

                    } while (!result.EndOfMessage);

                    ms.Seek(0, System.IO.SeekOrigin.Begin);
                    var msg = Encoding.UTF8.GetString(ms.ToArray());

                    // quick ignore empty
                    if (string.IsNullOrWhiteSpace(msg)) continue;

                    // try parse json
                    using var doc = JsonDocument.Parse(msg);
                    var root = doc.RootElement;

                    // Common shapes:
                    // { "channel": "subscriptionResponse", "data": {...} }
                    // { "channel": "activeAssetCtx", "data": { "ctx": { "funding": ... } } }
                    // { "channel": "error", "data": "..." }
                    // sometimes server sends plain array - ignore unless we know shape

                    if (root.TryGetProperty("channel", out var channelEl))
                    {
                        var channel = channelEl.GetString();

                        if (string.Equals(channel, "subscriptionResponse", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.Debug("[HL-WS] subscriptionResponse received.");
                            continue;
                        }

                        if (string.Equals(channel, "error", StringComparison.OrdinalIgnoreCase))
                        {
                            var data = root.GetProperty("data").ToString();
                            _logger.Debug("[HL-WS] Ignored message: " + msg);
                            _logger.Warn($"[HL-WS] Server error: {data}");
                            continue;
                        }

                        if (string.Equals(channel, "activeAssetCtx", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                // data.ctx.funding
                                var dataEl = root.GetProperty("data");
                                var ctx = dataEl.GetProperty("ctx");
                                double funding = ReadDoubleLenient(ctx.GetProperty("funding"));

                                // Some servers might send rate per-block or scaled — keep raw as-is
                                var fr = new FundingRate
                                {
                                    Exchange = "Hyperliquid",
                                    Symbol = "BTC-PERP",
                                    Rate = funding,
                                    Timestamp = DateTime.UtcNow,
                                    Source = "WS"
                                };

                                _logger.Debug($"[HL-WS] Received update funding BTC-PERP = {fr.Rate:E6}");
                                FundingEvent?.Invoke(fr);
                                continue;
                            }
                            catch (Exception ex)
                            {
                                _logger.Warn("[HL-WS] JSON parse error: " + ex.Message);
                                // continue loop (don't reconnect immediately)
                                continue;
                            }
                        }

                        // unknown channel — log debug and continue
                        _logger.Debug("[HL-WS] Ignored message: " + msg);
                        continue;
                    }
                    else
                    {
                        // No "channel" field — might be array or other shape.
                        _logger.Debug("[HL-WS] Ignored non-channel message.");
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    // shutting down
                    return;
                }
                catch (Exception ex)
                {
                    _logger.Warn("[HL-WS] Receive error → reconnect: " + ex.Message);
                    await ReconnectAsync();
                    return;
                }
            }
        }

        private static double ReadDoubleLenient(JsonElement el)
        {
            // Accept Number or String
            if (el.ValueKind == JsonValueKind.Number)
            {
                return el.GetDouble();
            }

            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (string.IsNullOrWhiteSpace(s)) return 0.0;
                // try invariant parse (server uses '.' decimal)
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    return v;
                // try fallback parse
                if (double.TryParse(s, out v))
                    return v;
                throw new FormatException($"Cannot parse numeric string '{s}'");
            }

            // other kinds (True/False/Null/Array/Object) - not supported
            throw new InvalidOperationException($"Unexpected JSON token kind: {el.ValueKind}");
        }

        private async Task HeartbeatLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (_ws.State == WebSocketState.Open)
                    {
                        // hyperliquid seems to expect "method":"ping" style instead of "op"
                        var ping = Encoding.UTF8.GetBytes("{\"method\":\"ping\"}");
                        await _ws.SendAsync(new ArraySegment<byte>(ping), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("[HL-WS] Ping fail → reconnect: " + ex.Message);
                    await ReconnectAsync();
                }

                await Task.Delay(15000);
            }
        }

        private async Task ReconnectAsync()
        {
            // Cancel any existing receive/heartbeat tasks
            try { _cts.Cancel(); } catch { }

            _cts = new CancellationTokenSource();

            try
            {
                _ws?.Dispose();
            }
            catch { }

            _ws = new ClientWebSocket();

            _logger.Warn("[HL-WS] Reconnecting...");

            await Task.Delay(1200);
            await ConnectAsync();

            // re-subscribe (best-effort)
            try
            {
                await SubscribeFundingAsync("BTC");
            }
            catch (Exception ex)
            {
                _logger.Warn("[HL-WS] Re-subscribe failed: " + ex.Message);
            }
        }
    }
}
