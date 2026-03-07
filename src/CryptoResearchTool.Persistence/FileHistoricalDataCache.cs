using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CryptoResearchTool.Persistence;

public class FileHistoricalDataCache : IHistoricalDataCache
{
    private readonly string _cacheDirectory;
    private readonly ILogger<FileHistoricalDataCache> _logger;

    public FileHistoricalDataCache(string cacheDirectory, ILogger<FileHistoricalDataCache> logger)
    {
        _cacheDirectory = cacheDirectory;
        _logger = logger;
        Directory.CreateDirectory(cacheDirectory);
    }

    private string GetCacheFilePath(string symbol, string timeframe, DateTime startDate, DateTime endDate)
    {
        var key = $"{symbol}_{timeframe}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
        return Path.Combine(_cacheDirectory, $"{key}.json");
    }

    public bool Exists(string symbol, string timeframe, DateTime startDate, DateTime endDate)
        => File.Exists(GetCacheFilePath(symbol, timeframe, startDate, endDate));

    public async Task<List<Candle>?> GetAsync(string symbol, string timeframe, DateTime startDate, DateTime endDate)
    {
        var path = GetCacheFilePath(symbol, timeframe, startDate, endDate);
        if (!File.Exists(path))
            return null;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            var candles = JsonConvert.DeserializeObject<List<Candle>>(json);
            _logger.LogInformation("Loaded {Count} candles from cache: {Path}", candles?.Count ?? 0, path);
            return candles;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read cache file {Path}", path);
            return null;
        }
    }

    public async Task SaveAsync(string symbol, string timeframe, DateTime startDate, DateTime endDate, List<Candle> candles)
    {
        var path = GetCacheFilePath(symbol, timeframe, startDate, endDate);
        try
        {
            var json = JsonConvert.SerializeObject(candles, Formatting.None);
            await File.WriteAllTextAsync(path, json);
            _logger.LogInformation("Cached {Count} candles to {Path}", candles.Count, path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write cache file {Path}", path);
        }
    }
}
