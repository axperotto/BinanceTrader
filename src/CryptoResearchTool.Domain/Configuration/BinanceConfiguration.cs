namespace CryptoResearchTool.Domain.Configuration;
public class BinanceConfiguration
{
    public string BaseWebSocketUrl { get; set; } = "wss://stream.binance.com:9443";
    public string BaseRestUrl { get; set; } = "https://api.binance.com";
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int MaxReconnectAttempts { get; set; } = 10;
}
