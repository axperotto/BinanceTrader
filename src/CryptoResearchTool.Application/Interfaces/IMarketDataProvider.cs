using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Interfaces;
public interface IMarketDataProvider
{
    event EventHandler<MarketTick>? TickReceived;
    event EventHandler<Candle>? CandleReceived;
    event EventHandler<bool>? ConnectionChanged;
    Task ConnectAsync(IEnumerable<string> symbols, IEnumerable<string> timeframes, CancellationToken ct);
    Task DisconnectAsync();
    bool IsConnected { get; }
}
