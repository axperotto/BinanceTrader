using System.Net.WebSockets;
using System.Text;
using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace CryptoResearchTool.Infrastructure;

public class BinanceWebSocketClient : IMarketDataProvider, IDisposable
{
    private readonly BinanceConfiguration _config;
    private readonly ILogger<BinanceWebSocketClient> _logger;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _isConnected;
    private IEnumerable<string> _symbols = Array.Empty<string>();
    private IEnumerable<string> _timeframes = Array.Empty<string>();

    public event EventHandler<MarketTick>? TickReceived;
    public event EventHandler<Candle>? CandleReceived;
    public event EventHandler<bool>? ConnectionChanged;
    public bool IsConnected => _isConnected;

    public BinanceWebSocketClient(BinanceConfiguration config, ILogger<BinanceWebSocketClient> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task ConnectAsync(IEnumerable<string> symbols, IEnumerable<string> timeframes, CancellationToken ct)
    {
        _symbols = symbols.ToList();
        _timeframes = timeframes.ToList();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await ConnectWithRetryAsync(_cts.Token);
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        int attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                attempt++;
                _logger.LogInformation("Connecting to Binance WebSocket (attempt {Attempt})", attempt);
                _ws?.Dispose();
                _ws = new ClientWebSocket();
                var streams = BuildStreamNames();
                var url = $"{_config.BaseWebSocketUrl}/stream?streams={string.Join("/", streams)}";
                await _ws.ConnectAsync(new Uri(url), ct);
                SetConnected(true);
                _logger.LogInformation("Connected to Binance WebSocket");
                attempt = 0;
                await ReceiveLoopAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WebSocket connection failed, retrying in {Delay}s", _config.ReconnectDelaySeconds);
                SetConnected(false);
                if (attempt >= _config.MaxReconnectAttempts)
                {
                    _logger.LogError("Max reconnect attempts reached");
                    break;
                }
                try { await Task.Delay(TimeSpan.FromSeconds(_config.ReconnectDelaySeconds), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private IEnumerable<string> BuildStreamNames()
    {
        var streams = new List<string>();
        foreach (var symbol in _symbols)
        {
            var s = symbol.ToLower();
            streams.Add($"{s}@trade");
            foreach (var tf in _timeframes)
                streams.Add($"{s}@kline_{tf}");
        }
        return streams;
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[65536];
        var sb = new StringBuilder();
        while (_ws!.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            sb.Clear();
            WebSocketReceiveResult result;
            do
            {
                result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
                    SetConnected(false);
                    return;
                }
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            try { ProcessMessage(sb.ToString()); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error processing WS message"); }
        }
        SetConnected(false);
    }

    private void ProcessMessage(string json)
    {
        var obj = JObject.Parse(json);
        var data = obj["data"];
        if (data == null) return;
        var eventType = data["e"]?.ToString();
        if (eventType == "trade")
        {
            var tick = new MarketTick(
                data["s"]!.ToString(),
                data["p"]!.Value<decimal>(),
                data["q"]!.Value<decimal>(),
                DateTimeOffset.FromUnixTimeMilliseconds(data["T"]!.Value<long>()).UtcDateTime
            );
            TickReceived?.Invoke(this, tick);
        }
        else if (eventType == "kline")
        {
            var k = data["k"]!;
            var candle = new Candle
            {
                Symbol = data["s"]!.ToString(),
                Timeframe = k["i"]!.ToString(),
                OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(k["t"]!.Value<long>()).UtcDateTime,
                CloseTime = DateTimeOffset.FromUnixTimeMilliseconds(k["T"]!.Value<long>()).UtcDateTime,
                Open = k["o"]!.Value<decimal>(),
                High = k["h"]!.Value<decimal>(),
                Low = k["l"]!.Value<decimal>(),
                Close = k["c"]!.Value<decimal>(),
                Volume = k["v"]!.Value<decimal>(),
                IsClosed = k["x"]!.Value<bool>()
            };
            CandleReceived?.Invoke(this, candle);
        }
    }

    private void SetConnected(bool connected)
    {
        _isConnected = connected;
        ConnectionChanged?.Invoke(this, connected);
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
        SetConnected(false);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _ws?.Dispose();
        GC.SuppressFinalize(this);
    }
}
