using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace CryptoResearchTool.Infrastructure;

public class BinanceHistoricalDataService : IHistoricalDataService, IDisposable
{
    private readonly BinanceConfiguration _config;
    private readonly ILogger<BinanceHistoricalDataService> _logger;
    private readonly HttpClient _httpClient;

    private const int MaxCandlesPerRequest = 1000;
    private const int RateLimitDelayMs = 250;
    private const int MaxRetries = 3;
    private const string KlinesEndpoint = "/api/v3/klines";

    public BinanceHistoricalDataService(BinanceConfiguration config, ILogger<BinanceHistoricalDataService> logger)
    {
        _config = config;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.BaseRestUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "CryptoResearchTool/1.0");
    }

    public async Task<List<Candle>> GetCandlesAsync(
        string symbol,
        string timeframe,
        DateTime startDate,
        DateTime endDate,
        IProgress<(int downloaded, int total, string message)>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Symbol cannot be empty.", nameof(symbol));
        if (string.IsNullOrWhiteSpace(timeframe)) throw new ArgumentException("Timeframe cannot be empty.", nameof(timeframe));
        if (endDate <= startDate) throw new ArgumentException("End date must be after start date.");

        var allCandles = new List<Candle>();
        var current = startDate;
        var intervalMs = GetIntervalMs(timeframe);
        var totalMs = (endDate - startDate).TotalMilliseconds;
        var estimatedTotal = Math.Max(1, (int)(totalMs / intervalMs));

        _logger.LogInformation("Downloading {Symbol} {Timeframe} from {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}",
            symbol, timeframe, startDate, endDate);
        progress?.Report((0, estimatedTotal, $"Starting download of {symbol} {timeframe}..."));

        int retryCount = 0;
        while (current < endDate && !ct.IsCancellationRequested)
        {
            try
            {
                var startMs = new DateTimeOffset(current, TimeSpan.Zero).ToUnixTimeMilliseconds();
                var endMs = new DateTimeOffset(endDate, TimeSpan.Zero).ToUnixTimeMilliseconds();
                var url = $"{KlinesEndpoint}?symbol={Uri.EscapeDataString(symbol)}&interval={Uri.EscapeDataString(timeframe)}&startTime={startMs}&endTime={endMs}&limit={MaxCandlesPerRequest}";

                var responseText = await _httpClient.GetStringAsync(url, ct);
                var jArray = JArray.Parse(responseText);

                if (jArray.Count == 0)
                    break;

                foreach (var item in jArray)
                {
                    var openTime = DateTimeOffset.FromUnixTimeMilliseconds(item[0]!.Value<long>()).UtcDateTime;
                    var closeTime = DateTimeOffset.FromUnixTimeMilliseconds(item[6]!.Value<long>()).UtcDateTime;
                    allCandles.Add(new Candle
                    {
                        Symbol = symbol,
                        Timeframe = timeframe,
                        OpenTime = openTime,
                        CloseTime = closeTime,
                        Open = item[1]!.Value<decimal>(),
                        High = item[2]!.Value<decimal>(),
                        Low = item[3]!.Value<decimal>(),
                        Close = item[4]!.Value<decimal>(),
                        Volume = item[5]!.Value<decimal>(),
                        IsClosed = true
                    });
                }

                var lastOpenTimeMs = jArray.Last![0]!.Value<long>();
                var lastOpenTime = DateTimeOffset.FromUnixTimeMilliseconds(lastOpenTimeMs).UtcDateTime;
                current = lastOpenTime.AddMilliseconds(intervalMs);
                retryCount = 0;

                progress?.Report((allCandles.Count, estimatedTotal,
                    $"Downloaded {allCandles.Count:N0} candles... ({current:yyyy-MM-dd})"));

                if (jArray.Count < MaxCandlesPerRequest)
                    break;

                await Task.Delay(RateLimitDelayMs, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Historical download cancelled.");
                break;
            }
            catch (HttpRequestException ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "HTTP error downloading candles, attempt {Attempt}/{Max}", retryCount, MaxRetries);
                if (retryCount >= MaxRetries)
                    throw new InvalidOperationException($"Failed to download candles after {MaxRetries} attempts: {ex.Message}", ex);
                await Task.Delay(TimeSpan.FromSeconds(retryCount * 2), ct);
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "Error downloading candles, attempt {Attempt}/{Max}", retryCount, MaxRetries);
                if (retryCount >= MaxRetries)
                    throw;
                await Task.Delay(TimeSpan.FromSeconds(retryCount * 2), ct);
            }
        }

        // Deduplicate and sort
        var result = allCandles
            .GroupBy(c => c.OpenTime)
            .Select(g => g.First())
            .OrderBy(c => c.OpenTime)
            .ToList();

        _logger.LogInformation("Downloaded {Count} candles for {Symbol} {Timeframe}", result.Count, symbol, timeframe);
        progress?.Report((result.Count, result.Count, $"Downloaded {result.Count:N0} candles."));
        return result;
    }

    private double GetIntervalMs(string timeframe)
    {
        var ms = timeframe switch
        {
            "1m" => 60_000,
            "3m" => 180_000,
            "5m" => 300_000,
            "15m" => 900_000,
            "30m" => 1_800_000,
            "1h" => 3_600_000,
            "2h" => 7_200_000,
            "4h" => 14_400_000,
            "6h" => 21_600_000,
            "8h" => 28_800_000,
            "12h" => 43_200_000,
            "1d" => 86_400_000,
            "3d" => 259_200_000,
            "1w" => 604_800_000,
            _ => -1
        };
        if (ms < 0)
        {
            _logger.LogWarning("Unrecognized timeframe '{Timeframe}', defaulting to 1m (60000ms).", timeframe);
            return 60_000;
        }
        return ms;
    }

    public void Dispose() => _httpClient.Dispose();
}
