using CryptoResearchTool.Domain.Models;

namespace CryptoResearchTool.Application.Interfaces;

public interface IHistoricalDataCache
{
    bool Exists(string symbol, string timeframe, DateTime startDate, DateTime endDate);
    Task<List<Candle>?> GetAsync(string symbol, string timeframe, DateTime startDate, DateTime endDate);
    Task SaveAsync(string symbol, string timeframe, DateTime startDate, DateTime endDate, List<Candle> candles);
}
