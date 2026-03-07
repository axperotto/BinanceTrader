namespace CryptoResearchTool.Domain.Configuration;
public class AppConfiguration
{
    public BinanceConfiguration Binance { get; set; } = new();
    public SimulationConfiguration Simulation { get; set; } = new();
    public HistoricalAnalysisConfiguration Historical { get; set; } = new();
    public List<string> Symbols { get; set; } = new();
    public List<StrategyConfiguration> Strategies { get; set; } = new();
    public string DatabasePath { get; set; } = "data/cryptoresearch.db";
    public string LogPath { get; set; } = "logs/";
    public string RunName { get; set; } = "";
    public int MetricsSnapshotIntervalSeconds { get; set; } = 60;
}
