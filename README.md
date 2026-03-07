# CryptoResearchTool — Binance Algorithmic Trading Research Simulator

A **read-only** crypto trading research simulator built in C# .NET 8 with WinForms.  
Connects to Binance via WebSocket, runs multiple strategies in parallel on virtual wallets, records everything in SQLite, and displays live metrics in a WinForms dashboard.

> **No real orders are ever sent.** The tool uses only public Binance market data endpoints.

---

## Features

- **Live data** via Binance WebSocket (trade stream + kline/candlestick streams)
- **5 built-in strategies** running in parallel on independent virtual wallets
  - Moving Average Crossover
  - RSI Mean Reversion
  - Breakout
  - Momentum
  - Buy & Hold (benchmark)
- **Portfolio simulation** with realistic fee (0.1%) and slippage (0.05%) modelling
- **Metrics engine**: win rate, profit factor, max drawdown, Sharpe ratio, expectancy, and more
- **SQLite persistence**: all trades, signals, equity history and metric snapshots saved locally
- **WinForms dashboard**: strategy grid with colour-coded PnL, log panel, strategy detail view
- **CSV export** of trade history
- **Configurable** via `appsettings.json` and `strategies.json` — supports multiple simultaneous instances
- **Auto-reconnect** on WebSocket disconnection, designed for 24/7 operation

---

## Solution Structure

```
src/
├── CryptoResearchTool.Domain/          # Pure domain models & configuration
│   ├── Models/                         # MarketTick, Candle, SimulatedTrade, StrategyMetrics, …
│   └── Configuration/                  # AppConfiguration, SimulationConfiguration, …
│
├── CryptoResearchTool.Application/     # Application logic & interfaces
│   ├── Interfaces/                     # IMarketDataProvider, ITradingStrategy, IPortfolioSimulator, …
│   ├── Services/                       # PortfolioSimulator, MetricsCalculator, StrategyRunner
│   └── Strategies/                     # 5 strategy implementations + StrategyFactory
│
├── CryptoResearchTool.Infrastructure/  # Binance WebSocket client & reconnect logic
│   └── BinanceWebSocketClient.cs
│
├── CryptoResearchTool.Persistence/     # SQLite repository (raw ADO.NET)
│   └── SqliteRepository.cs
│
└── CryptoResearchTool.UI/              # WinForms application (Windows only)
    ├── MainForm.cs                     # Dashboard: strategy grid, log panel, controls
    ├── StrategyDetailForm.cs           # Per-strategy metrics & trade history
    ├── Program.cs                      # DI wiring, config loading
    ├── appsettings.json                # Binance, database, logging settings
    └── strategies.json                 # Symbols, simulation params, strategy list
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) (8.0.x)
- **Windows** to run the WinForms UI (the class library projects are cross-platform)
- Internet access to reach `wss://stream.binance.com:9443` (public endpoint, no API key required)

---

## Build

```bash
cd src
dotnet build CryptoResearchTool.slnx --configuration Release
```

Cross-platform class libraries only (no GUI):

```bash
cd src
dotnet build CryptoResearchTool.Domain CryptoResearchTool.Application \
             CryptoResearchTool.Infrastructure CryptoResearchTool.Persistence
```

---

## Run

```bash
cd src/CryptoResearchTool.UI
dotnet run
```

Or publish and run the executable:

```bash
cd src
dotnet publish CryptoResearchTool.UI/CryptoResearchTool.UI.csproj \
    -c Release -r win-x64 --self-contained -o publish/
./publish/CryptoResearchTool.UI.exe
```

---

## Configuration

### `appsettings.json`

Placed next to the executable. Controls Binance connection, database path, log path, and snapshot interval.

```json
{
  "Binance": {
    "BaseWebSocketUrl": "wss://stream.binance.com:9443",
    "ReconnectDelaySeconds": 5,
    "MaxReconnectAttempts": 10
  },
  "DatabasePath": "data/cryptoresearch.db",
  "LogPath":      "logs/",
  "MetricsSnapshotIntervalSeconds": 60
}
```

### `strategies.json`

Controls the simulation parameters and the list of active strategies.

```json
{
  "Simulation": {
    "InitialCapital": 1000.0,
    "FeePercent": 0.1,
    "SlippagePercent": 0.05,
    "DefaultPositionSizePercent": 100.0
  },
  "Symbols": ["BTCUSDT", "ETHUSDT", "BNBUSDT"],
  "Strategies": [
    {
      "Type": "MovingAverageCrossover",
      "Name": "MA_BTC_5_20",
      "Symbol": "BTCUSDT",
      "Timeframe": "1m",
      "Enabled": true,
      "Parameters": { "FastPeriod": 5, "SlowPeriod": 20 }
    }
  ]
}
```

Supported strategy types: `MovingAverageCrossover`, `RsiMeanReversion`, `Breakout`, `Momentum`, `BuyAndHold`.

---

## Multi-Instance

Run multiple isolated instances simultaneously with different configs:

```bash
# Instance A — aggressive MA strategies
CryptoResearchTool.UI.exe --appsettings appsettings-a.json

# Instance B — conservative RSI strategies
CryptoResearchTool.UI.exe --appsettings appsettings-b.json
```

Each instance has its own `DatabasePath` and `LogPath` so they never interfere.

---

## Adding a New Strategy

1. Add a class in `CryptoResearchTool.Application/Strategies/` that extends `BaseStrategy`.
2. Override `Evaluate()` to return a `StrategySignal` on each closed candle.
3. Register the type in `StrategyFactory.Create()`.
4. Add a new entry in `strategies.json`.

---

## SQLite Schema

| Table                | Contents                                    |
|----------------------|---------------------------------------------|
| `RunSessions`        | Each launch of the tool                     |
| `StrategyRuns`       | Each active strategy within a run           |
| `StrategySignals`    | Every generated Buy/Sell signal             |
| `SimulatedTrades`    | Completed virtual trades with PnL           |
| `EquityHistory`      | Periodic equity snapshots per strategy      |
| `MetricSnapshots`    | Periodic full-metrics snapshots             |
| `ApplicationLogs`    | Structured application log entries          |

---

## Disclaimer

This tool is a **research simulator only**. It does not place real orders and does not require any Binance API key. Statistical results from simulation are not financial advice and past simulated performance does not guarantee future real performance.
