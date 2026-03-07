using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace CryptoResearchTool.Persistence;

public class SqliteRepository : IPersistenceRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteRepository> _logger;

    public SqliteRepository(string dbPath, ILogger<SqliteRepository> logger)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
        _connectionString = $"Data Source={dbPath}";
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var sql = @"
CREATE TABLE IF NOT EXISTS RunSessions (
    Id TEXT PRIMARY KEY, Name TEXT, StartTime TEXT, ConfigJson TEXT
);
CREATE TABLE IF NOT EXISTS StrategyRuns (
    Id TEXT PRIMARY KEY, RunSessionId TEXT, StrategyName TEXT, StrategyType TEXT,
    Symbol TEXT, Timeframe TEXT, StartTime TEXT, ConfigJson TEXT
);
CREATE TABLE IF NOT EXISTS StrategySignals (
    Id TEXT PRIMARY KEY, StrategyRunId TEXT, Symbol TEXT, SignalType TEXT,
    Price REAL, Timestamp TEXT, Reason TEXT
);
CREATE TABLE IF NOT EXISTS SimulatedTrades (
    Id TEXT PRIMARY KEY, StrategyRunId TEXT, Symbol TEXT, EntryPrice REAL, ExitPrice REAL,
    Quantity REAL, PnL REAL, PnLPercent REAL, TotalFees REAL,
    EntryTime TEXT, ExitTime TEXT, HoldingSeconds REAL, EntryReason TEXT, ExitReason TEXT
);
CREATE TABLE IF NOT EXISTS EquityHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT, StrategyRunId TEXT, Timestamp TEXT,
    Equity REAL, Cash REAL, UnrealizedPnL REAL
);
CREATE TABLE IF NOT EXISTS MetricSnapshots (
    Id INTEGER PRIMARY KEY AUTOINCREMENT, StrategyRunId TEXT, StrategyName TEXT,
    Timestamp TEXT, TotalTrades INTEGER, WinRate REAL, NetProfit REAL,
    ReturnPercent REAL, MaxDrawdownPercent REAL, SharpeRatio REAL, CurrentEquity REAL
);
CREATE TABLE IF NOT EXISTS ApplicationLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp TEXT, Level TEXT, Message TEXT, Exception TEXT
);
";
        using var cmd = new SqliteCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string> CreateRunSessionAsync(string runName, string configJson)
    {
        var id = Guid.NewGuid().ToString();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqliteCommand("INSERT INTO RunSessions VALUES(@Id,@Name,@Start,@Config)", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Name", runName);
        cmd.Parameters.AddWithValue("@Start", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@Config", configJson);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    public async Task<string> CreateStrategyRunAsync(string runSessionId, string strategyName, string strategyType, string symbol, string timeframe, string configJson)
    {
        var id = Guid.NewGuid().ToString();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqliteCommand("INSERT INTO StrategyRuns VALUES(@Id,@Sid,@Name,@Type,@Sym,@Tf,@Start,@Config)", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Sid", runSessionId);
        cmd.Parameters.AddWithValue("@Name", strategyName);
        cmd.Parameters.AddWithValue("@Type", strategyType);
        cmd.Parameters.AddWithValue("@Sym", symbol);
        cmd.Parameters.AddWithValue("@Tf", timeframe);
        cmd.Parameters.AddWithValue("@Start", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@Config", configJson);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    public async Task SaveSignalAsync(string strategyRunId, StrategySignal signal)
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqliteCommand("INSERT INTO StrategySignals VALUES(@Id,@RunId,@Sym,@Type,@Price,@Ts,@Reason)", conn);
            cmd.Parameters.AddWithValue("@Id", signal.Id.ToString());
            cmd.Parameters.AddWithValue("@RunId", strategyRunId);
            cmd.Parameters.AddWithValue("@Sym", signal.Symbol);
            cmd.Parameters.AddWithValue("@Type", signal.Type.ToString());
            cmd.Parameters.AddWithValue("@Price", (double)signal.Price);
            cmd.Parameters.AddWithValue("@Ts", signal.Timestamp.ToString("O"));
            cmd.Parameters.AddWithValue("@Reason", signal.Reason);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "SaveSignal failed"); }
    }

    public async Task SaveTradeAsync(SimulatedTrade trade)
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqliteCommand(@"INSERT INTO SimulatedTrades VALUES(@Id,@RunId,@Sym,@Entry,@Exit,@Qty,@PnL,@PnLPct,@Fees,@EntryT,@ExitT,@Hold,@EntryR,@ExitR)", conn);
            cmd.Parameters.AddWithValue("@Id", trade.Id.ToString());
            cmd.Parameters.AddWithValue("@RunId", trade.StrategyRunId);
            cmd.Parameters.AddWithValue("@Sym", trade.Symbol);
            cmd.Parameters.AddWithValue("@Entry", (double)trade.EntryPrice);
            cmd.Parameters.AddWithValue("@Exit", (double)trade.ExitPrice);
            cmd.Parameters.AddWithValue("@Qty", (double)trade.Quantity);
            cmd.Parameters.AddWithValue("@PnL", (double)trade.PnL);
            cmd.Parameters.AddWithValue("@PnLPct", (double)trade.PnLPercent);
            cmd.Parameters.AddWithValue("@Fees", (double)trade.TotalFees);
            cmd.Parameters.AddWithValue("@EntryT", trade.EntryTime.ToString("O"));
            cmd.Parameters.AddWithValue("@ExitT", trade.ExitTime.ToString("O"));
            cmd.Parameters.AddWithValue("@Hold", trade.HoldingTime.TotalSeconds);
            cmd.Parameters.AddWithValue("@EntryR", trade.EntryReason);
            cmd.Parameters.AddWithValue("@ExitR", trade.ExitReason);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "SaveTrade failed"); }
    }

    public async Task SaveEquityPointAsync(string strategyRunId, EquityPoint point)
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqliteCommand("INSERT INTO EquityHistory(StrategyRunId,Timestamp,Equity,Cash,UnrealizedPnL) VALUES(@RunId,@Ts,@Eq,@Cash,@Upnl)", conn);
            cmd.Parameters.AddWithValue("@RunId", strategyRunId);
            cmd.Parameters.AddWithValue("@Ts", point.Timestamp.ToString("O"));
            cmd.Parameters.AddWithValue("@Eq", (double)point.Equity);
            cmd.Parameters.AddWithValue("@Cash", (double)point.Cash);
            cmd.Parameters.AddWithValue("@Upnl", (double)point.UnrealizedPnL);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "SaveEquityPoint failed"); }
    }

    public async Task SaveMetricsSnapshotAsync(StrategyMetrics metrics)
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqliteCommand(@"INSERT INTO MetricSnapshots(StrategyRunId,StrategyName,Timestamp,TotalTrades,WinRate,NetProfit,ReturnPercent,MaxDrawdownPercent,SharpeRatio,CurrentEquity)
VALUES(@RunId,@Name,@Ts,@Trades,@WR,@NP,@Ret,@DD,@SR,@Eq)", conn);
            cmd.Parameters.AddWithValue("@RunId", metrics.StrategyRunId);
            cmd.Parameters.AddWithValue("@Name", metrics.StrategyName);
            cmd.Parameters.AddWithValue("@Ts", metrics.LastUpdated.ToString("O"));
            cmd.Parameters.AddWithValue("@Trades", metrics.TotalTrades);
            cmd.Parameters.AddWithValue("@WR", (double)metrics.WinRate);
            cmd.Parameters.AddWithValue("@NP", (double)metrics.NetProfit);
            cmd.Parameters.AddWithValue("@Ret", (double)metrics.ReturnPercent);
            cmd.Parameters.AddWithValue("@DD", (double)metrics.MaxDrawdownPercent);
            cmd.Parameters.AddWithValue("@SR", (double)metrics.SharpeRatio);
            cmd.Parameters.AddWithValue("@Eq", (double)metrics.CurrentEquity);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "SaveMetricsSnapshot failed"); }
    }

    public async Task SaveApplicationLogAsync(string level, string message, string? exception = null)
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqliteCommand("INSERT INTO ApplicationLogs(Timestamp,Level,Message,Exception) VALUES(@Ts,@Lvl,@Msg,@Ex)", conn);
            cmd.Parameters.AddWithValue("@Ts", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("@Lvl", level);
            cmd.Parameters.AddWithValue("@Msg", message);
            cmd.Parameters.AddWithValue("@Ex", exception ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* don't recurse */ }
    }

    public async Task<List<SimulatedTrade>> GetTradesAsync(string strategyRunId)
    {
        var trades = new List<SimulatedTrade>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqliteCommand("SELECT * FROM SimulatedTrades WHERE StrategyRunId=@RunId ORDER BY EntryTime", conn);
        cmd.Parameters.AddWithValue("@RunId", strategyRunId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            trades.Add(new SimulatedTrade
            {
                Id = Guid.Parse(reader.GetString(0)),
                StrategyRunId = reader.GetString(1),
                Symbol = reader.GetString(2),
                EntryPrice = (decimal)reader.GetDouble(3),
                ExitPrice = (decimal)reader.GetDouble(4),
                Quantity = (decimal)reader.GetDouble(5),
                PnL = (decimal)reader.GetDouble(6),
                PnLPercent = (decimal)reader.GetDouble(7),
                TotalFees = (decimal)reader.GetDouble(8),
                EntryTime = DateTime.Parse(reader.GetString(9)),
                ExitTime = DateTime.Parse(reader.GetString(10)),
                HoldingTime = TimeSpan.FromSeconds(reader.GetDouble(11)),
                EntryReason = reader.GetString(12),
                ExitReason = reader.GetString(13)
            });
        }
        return trades;
    }

    public async Task<List<EquityPoint>> GetEquityHistoryAsync(string strategyRunId)
    {
        var points = new List<EquityPoint>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqliteCommand("SELECT Timestamp,Equity,Cash,UnrealizedPnL FROM EquityHistory WHERE StrategyRunId=@RunId ORDER BY Timestamp", conn);
        cmd.Parameters.AddWithValue("@RunId", strategyRunId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            points.Add(new EquityPoint
            {
                Timestamp = DateTime.Parse(reader.GetString(0)),
                Equity = (decimal)reader.GetDouble(1),
                Cash = (decimal)reader.GetDouble(2),
                UnrealizedPnL = (decimal)reader.GetDouble(3)
            });
        }
        return points;
    }
}
