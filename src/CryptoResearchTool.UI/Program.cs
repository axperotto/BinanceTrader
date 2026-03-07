using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Application.Services;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Infrastructure;
using CryptoResearchTool.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;

namespace CryptoResearchTool.UI;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Load appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        var logPath = configuration["LogPath"] ?? "logs/";
        Directory.CreateDirectory(logPath);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logPath, "cryptoresearch-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var appConfig = LoadAppConfiguration(configuration);

        var services = new ServiceCollection();
        services.AddSingleton(appConfig);
        services.AddSingleton(appConfig.Binance);
        services.AddSingleton(appConfig.Simulation);
        services.AddSingleton(appConfig.Historical);
        services.AddLogging(b => b.AddSerilog(dispose: true));
        services.AddSingleton<IMarketDataProvider>(sp =>
            new BinanceWebSocketClient(appConfig.Binance, sp.GetRequiredService<ILogger<BinanceWebSocketClient>>()));
        services.AddSingleton<IHistoricalDataService>(sp =>
            new BinanceHistoricalDataService(appConfig.Binance, sp.GetRequiredService<ILogger<BinanceHistoricalDataService>>()));
        services.AddSingleton<IHistoricalDataCache>(sp =>
            new FileHistoricalDataCache(appConfig.Historical.CacheDirectory, sp.GetRequiredService<ILogger<FileHistoricalDataCache>>()));
        services.AddSingleton<IPersistenceRepository>(sp =>
            new SqliteRepository(appConfig.DatabasePath, sp.GetRequiredService<ILogger<SqliteRepository>>()));
        services.AddSingleton<IMetricsCalculator, MetricsCalculator>();
        services.AddSingleton<HistoricalBacktestEngine>();
        services.AddSingleton<MainForm>();

        var provider = services.BuildServiceProvider();

        Log.Information("CryptoResearchTool starting. Run: {RunName}", appConfig.RunName);
        System.Windows.Forms.Application.Run(provider.GetRequiredService<MainForm>());

        Log.CloseAndFlush();
    }

    private static AppConfiguration LoadAppConfiguration(IConfiguration configuration)
    {
        var appConfig = new AppConfiguration();

        // Bind from appsettings.json
        configuration.GetSection("Binance").Bind(appConfig.Binance);
        configuration.GetSection("Simulation").Bind(appConfig.Simulation);
        configuration.GetSection("Historical").Bind(appConfig.Historical);
        appConfig.DatabasePath = configuration["DatabasePath"] ?? appConfig.DatabasePath;
        appConfig.LogPath = configuration["LogPath"] ?? appConfig.LogPath;
        appConfig.MetricsSnapshotIntervalSeconds = int.TryParse(
            configuration["MetricsSnapshotIntervalSeconds"], out var msi) ? msi : appConfig.MetricsSnapshotIntervalSeconds;

        // Load strategies.json
        var strategiesPath = Path.Combine(AppContext.BaseDirectory, "strategies.json");
        if (File.Exists(strategiesPath))
        {
            try
            {
                var json = File.ReadAllText(strategiesPath);
                var stratConfig = JsonConvert.DeserializeObject<StrategiesFileModel>(json);
                if (stratConfig != null)
                {
                    if (stratConfig.Simulation != null)
                        appConfig.Simulation = stratConfig.Simulation;
                    if (stratConfig.Symbols?.Count > 0)
                        appConfig.Symbols = stratConfig.Symbols;
                    if (stratConfig.Strategies?.Count > 0)
                    {
                        foreach (var s in stratConfig.Strategies)
                            s.Normalize();
                        appConfig.Strategies = stratConfig.Strategies;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load strategies.json, using defaults");
            }
        }

        if (string.IsNullOrWhiteSpace(appConfig.RunName))
            appConfig.RunName = $"Run_{DateTime.Now:yyyyMMdd_HHmmss}";

        return appConfig;
    }

    private sealed class StrategiesFileModel
    {
        public SimulationConfiguration? Simulation { get; set; }
        public List<string>? Symbols { get; set; }
        public List<StrategyConfiguration>? Strategies { get; set; }
    }
}
