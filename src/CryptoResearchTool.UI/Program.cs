using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Application.Services;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Infrastructure;
using CryptoResearchTool.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CryptoResearchTool.UI;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/cryptoresearch-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var services = new ServiceCollection();

        var appConfig = new AppConfiguration
        {
            Binance = new BinanceConfiguration(),
            Simulation = new SimulationConfiguration { InitialCapital = 1000m },
            Symbols = new List<string> { "BTCUSDT" },
            Strategies = new List<StrategyConfiguration>
            {
                new() { Type = "MovingAverageCrossover", Name = "MA Cross", Symbol = "BTCUSDT", Timeframe = "1m",
                    Parameters = new Dictionary<string, object> { ["FastPeriod"] = 5, ["SlowPeriod"] = 20 } },
                new() { Type = "RsiMeanReversion", Name = "RSI MR", Symbol = "BTCUSDT", Timeframe = "1m",
                    Parameters = new Dictionary<string, object> { ["RsiPeriod"] = 14, ["Oversold"] = 30m, ["Overbought"] = 70m } },
                new() { Type = "BuyAndHold", Name = "Buy & Hold", Symbol = "BTCUSDT", Timeframe = "1m",
                    Parameters = new Dictionary<string, object>() }
            },
            RunName = $"Run_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        services.AddSingleton(appConfig);
        services.AddSingleton(appConfig.Binance);
        services.AddSingleton(appConfig.Simulation);
        services.AddLogging(b => b.AddSerilog(dispose: true));
        services.AddSingleton<IMarketDataProvider>(sp =>
            new BinanceWebSocketClient(appConfig.Binance, sp.GetRequiredService<ILogger<BinanceWebSocketClient>>()));
        services.AddSingleton<IPersistenceRepository>(sp =>
            new SqliteRepository(appConfig.DatabasePath, sp.GetRequiredService<ILogger<SqliteRepository>>()));
        services.AddSingleton<IMetricsCalculator, MetricsCalculator>();
        services.AddSingleton<MainForm>();

        var provider = services.BuildServiceProvider();

        Application.Run(provider.GetRequiredService<MainForm>());
    }
}
