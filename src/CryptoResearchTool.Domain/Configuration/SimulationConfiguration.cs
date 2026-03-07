namespace CryptoResearchTool.Domain.Configuration;
public class SimulationConfiguration
{
    public decimal InitialCapital { get; set; } = 1000.0m;
    public decimal FeePercent { get; set; } = 0.1m;
    public decimal SlippagePercent { get; set; } = 0.05m;
    public decimal DefaultPositionSizePercent { get; set; } = 100.0m;
}
