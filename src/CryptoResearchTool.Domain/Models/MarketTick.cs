namespace CryptoResearchTool.Domain.Models;
public record MarketTick(string Symbol, decimal Price, decimal Quantity, DateTime Timestamp);
