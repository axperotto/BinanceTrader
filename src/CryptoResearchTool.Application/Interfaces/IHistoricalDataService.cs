using CryptoResearchTool.Domain.Models;

namespace CryptoResearchTool.Application.Interfaces;

public interface IHistoricalDataService
{
    Task<List<Candle>> GetCandlesAsync(
        string symbol,
        string timeframe,
        DateTime startDate,
        DateTime endDate,
        IProgress<(int downloaded, int total, string message)>? progress = null,
        CancellationToken ct = default);
}
