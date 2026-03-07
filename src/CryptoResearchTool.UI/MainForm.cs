using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Application.Services;
using CryptoResearchTool.Application.Strategies;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CryptoResearchTool.UI;

public partial class MainForm : Form
{
    private readonly AppConfiguration _config;
    private readonly IMarketDataProvider _marketData;
    private readonly IPersistenceRepository _repository;
    private readonly IMetricsCalculator _metricsCalculator;
    private readonly ILogger<MainForm> _logger;

    private readonly List<StrategyRunner> _runners = new();
    private CancellationTokenSource? _cts;
    private System.Windows.Forms.Timer? _refreshTimer;
    private DateTime _startTime;
    private string _runSessionId = "";

    private DataGridView _gridStrategies = null!;
    private RichTextBox _logBox = null!;
    private Label _lblStatus = null!;
    private Label _lblUptime = null!;
    private Label _lblRunName = null!;
    private Button _btnStart = null!;
    private Button _btnStop = null!;
    private Button _btnOpenFolder = null!;
    private Button _btnExportCsv = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _tsslConnection = null!;

    public MainForm(AppConfiguration config, IMarketDataProvider marketData,
        IPersistenceRepository repository, IMetricsCalculator metricsCalculator,
        ILogger<MainForm> logger)
    {
        _config = config;
        _marketData = marketData;
        _repository = repository;
        _metricsCalculator = metricsCalculator;
        _logger = logger;
        InitializeComponent();
        WireEvents();
    }

    private void InitializeComponent()
    {
        Text = "Crypto Research Tool - Simulation Mode";
        Size = new Size(1200, 750);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(8) };
        _btnStart = new Button { Text = "Start", Width = 90, Height = 36, Left = 8, Top = 12, BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnStop = new Button { Text = "Stop", Width = 90, Height = 36, Left = 106, Top = 12, BackColor = Color.FromArgb(220, 53, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };
        _btnOpenFolder = new Button { Text = "Folder", Width = 90, Height = 36, Left = 204, Top = 12, FlatStyle = FlatStyle.Flat };
        _btnExportCsv = new Button { Text = "Export CSV", Width = 110, Height = 36, Left = 302, Top = 12, FlatStyle = FlatStyle.Flat };
        _lblRunName = new Label { Text = "Run: -", AutoSize = true, Left = 430, Top = 20, Font = new Font("Segoe UI", 9f) };
        _lblUptime = new Label { Text = "Uptime: 00:00:00", AutoSize = true, Left = 620, Top = 20, Font = new Font("Segoe UI", 9f) };
        _lblStatus = new Label { Text = "", AutoSize = true, Left = 800, Top = 20, Font = new Font("Segoe UI", 9f) };
        topPanel.Controls.AddRange(new Control[] { _btnStart, _btnStop, _btnOpenFolder, _btnExportCsv, _lblRunName, _lblUptime, _lblStatus });

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 420 };

        _gridStrategies = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false
        };
        _gridStrategies.Columns.AddRange(
            new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Strategy", FillWeight = 120 },
            new DataGridViewTextBoxColumn { Name = "colSymbol", HeaderText = "Symbol", FillWeight = 70 },
            new DataGridViewTextBoxColumn { Name = "colTf", HeaderText = "TF", FillWeight = 40 },
            new DataGridViewTextBoxColumn { Name = "colEquity", HeaderText = "Equity ($)", FillWeight = 80 },
            new DataGridViewTextBoxColumn { Name = "colReturn", HeaderText = "Return %", FillWeight = 70 },
            new DataGridViewTextBoxColumn { Name = "colTrades", HeaderText = "Trades", FillWeight = 50 },
            new DataGridViewTextBoxColumn { Name = "colWinRate", HeaderText = "Win %", FillWeight = 60 },
            new DataGridViewTextBoxColumn { Name = "colPnL", HeaderText = "Net PnL ($)", FillWeight = 80 },
            new DataGridViewTextBoxColumn { Name = "colDD", HeaderText = "Max DD %", FillWeight = 70 },
            new DataGridViewTextBoxColumn { Name = "colSharpe", HeaderText = "Sharpe", FillWeight = 60 },
            new DataGridViewTextBoxColumn { Name = "colSignal", HeaderText = "Last Signal", FillWeight = 100 },
            new DataGridViewTextBoxColumn { Name = "colStatus", HeaderText = "Status", FillWeight = 100 }
        );
        split.Panel1.Controls.Add(_gridStrategies);

        var logLabel = new Label { Text = " Application Log", Dock = DockStyle.Top, Height = 22, BackColor = Color.FromArgb(52, 58, 64), ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _logBox = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.LightGreen, Font = new Font("Consolas", 9f), ScrollBars = RichTextBoxScrollBars.Vertical };
        split.Panel2.Controls.Add(_logBox);
        split.Panel2.Controls.Add(logLabel);

        _statusStrip = new StatusStrip();
        _tsslConnection = new ToolStripStatusLabel("Disconnected") { ForeColor = Color.Red };
        _statusStrip.Items.Add(_tsslConnection);
        _statusStrip.Items.Add(new ToolStripStatusLabel("|"));
        _statusStrip.Items.Add(new ToolStripStatusLabel("Crypto Research Tool v1.0 - Simulation Mode"));

        Controls.Add(split);
        Controls.Add(topPanel);
        Controls.Add(_statusStrip);
    }

    private void WireEvents()
    {
        _btnStart.Click += async (s, e) => await StartAsync();
        _btnStop.Click += async (s, e) => await StopAsync();
        _btnOpenFolder.Click += (s, e) => OpenDataFolder();
        _btnExportCsv.Click += (s, e) => ExportCsv();
        _gridStrategies.CellDoubleClick += (s, e) => ShowStrategyDetail(e.RowIndex);
        _marketData.ConnectionChanged += (s, e) => Invoke(() => UpdateConnectionStatus(e));
        FormClosing += async (s, e) => { if (_cts != null) await StopAsync(); };
    }

    private async Task StartAsync()
    {
        try
        {
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _cts = new CancellationTokenSource();
            _startTime = DateTime.UtcNow;

            await _repository.InitializeAsync();
            _runSessionId = await _repository.CreateRunSessionAsync(_config.RunName, "{}");
            _lblRunName.Text = $"Run: {_config.RunName}";

            _runners.Clear();
            _gridStrategies.Rows.Clear();

            foreach (var stratConfig in _config.Strategies.Where(s => s.Enabled))
            {
                var strategy = StrategyFactory.Create(stratConfig, _config.Simulation);
                var runId = await _repository.CreateStrategyRunAsync(_runSessionId, stratConfig.Name, stratConfig.Type, stratConfig.Symbol, stratConfig.Timeframe, "{}");
                strategy.Initialize(runId);
                var portfolio = new PortfolioSimulator(_config.Simulation,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<PortfolioSimulator>.Instance,
                    runId, _config.Simulation.InitialCapital);
                var runner = new StrategyRunner(runId, strategy, portfolio, _metricsCalculator, _repository, _config.Simulation,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
                _runners.Add(runner);
                _gridStrategies.Rows.Add(stratConfig.Name, stratConfig.Symbol, stratConfig.Timeframe, "-", "-", "0", "-", "-", "-", "-", "-", "Waiting");
            }

            _marketData.TickReceived += OnTickReceived;
            _marketData.CandleReceived += OnCandleReceived;

            var timeframes = _config.Strategies.Select(s => s.Timeframe).Distinct();
            _ = Task.Run(async () => await _marketData.ConnectAsync(_config.Symbols, timeframes, _cts.Token));

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _refreshTimer.Tick += async (s, e) => await RefreshUiAsync();
            _refreshTimer.Start();

            AppendLog("INFO", $"Started run: {_config.RunName} with {_runners.Count} strategies");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start failed");
            AppendLog("ERROR", $"Start failed: {ex.Message}");
            _btnStart.Enabled = true;
            _btnStop.Enabled = false;
        }
    }

    private async Task StopAsync()
    {
        _refreshTimer?.Stop();
        _cts?.Cancel();
        _marketData.TickReceived -= OnTickReceived;
        _marketData.CandleReceived -= OnCandleReceived;
        await _marketData.DisconnectAsync();

        foreach (var runner in _runners)
            await runner.UpdateMetricsAsync();

        _btnStart.Enabled = true;
        _btnStop.Enabled = false;
        AppendLog("INFO", "Stopped.");
    }

    private void OnTickReceived(object? sender, MarketTick tick)
    {
        foreach (var r in _runners) r.OnTick(tick);
    }

    private void OnCandleReceived(object? sender, Candle candle)
    {
        foreach (var r in _runners) r.OnCandle(candle);
    }

    private async Task RefreshUiAsync()
    {
        foreach (var runner in _runners)
            await runner.UpdateMetricsAsync();

        if (InvokeRequired)
            Invoke(UpdateGrid);
        else
            UpdateGrid();

        var uptime = DateTime.UtcNow - _startTime;
        if (InvokeRequired)
            Invoke(() => _lblUptime.Text = $"Uptime: {uptime:hh\\:mm\\:ss}");
        else
            _lblUptime.Text = $"Uptime: {uptime:hh\\:mm\\:ss}";
    }

    private void UpdateGrid()
    {
        for (int i = 0; i < _runners.Count && i < _gridStrategies.Rows.Count; i++)
        {
            var m = _runners[i].CurrentMetrics;
            var state = _runners[i].Strategy.GetState();
            var row = _gridStrategies.Rows[i];
            row.Cells["colEquity"].Value = $"{m.CurrentEquity:F2}";
            row.Cells["colReturn"].Value = $"{m.ReturnPercent:F2}%";
            row.Cells["colTrades"].Value = m.TotalTrades.ToString();
            row.Cells["colWinRate"].Value = m.TotalTrades > 0 ? $"{m.WinRate:F1}%" : "-";
            row.Cells["colPnL"].Value = $"{m.NetProfit:F2}";
            row.Cells["colDD"].Value = $"{m.MaxDrawdownPercent:F2}%";
            row.Cells["colSharpe"].Value = $"{m.SharpeRatio:F2}";
            row.Cells["colSignal"].Value = state.LastSignal != null ? $"{state.LastSignal.Type} @ {state.LastSignal.Price:F2}" : "-";
            row.Cells["colStatus"].Value = state.OpenPosition != null ? $"OPEN {state.OpenPosition.Symbol} UPnL:{state.OpenPosition.UnrealizedPnL:F2}" : "No Position";
            row.DefaultCellStyle.BackColor = m.NetProfit >= 0 ? Color.FromArgb(230, 255, 230) : Color.FromArgb(255, 230, 230);
        }
    }

    private void UpdateConnectionStatus(bool connected)
    {
        _tsslConnection.Text = connected ? "Connected" : "Disconnected";
        _tsslConnection.ForeColor = connected ? Color.Green : Color.Red;
    }

    private void ShowStrategyDetail(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _runners.Count) return;
        var runner = _runners[rowIndex];
        using var form = new StrategyDetailForm(runner);
        form.ShowDialog(this);
    }

    private void OpenDataFolder()
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(_config.DatabasePath)) ?? ".";
        Directory.CreateDirectory(dir);
        try { System.Diagnostics.Process.Start("explorer.exe", dir); } catch { }
    }

    private void ExportCsv()
    {
        if (!_runners.Any()) { MessageBox.Show("No strategies running.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"trades_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        static string CsvField(string s) => s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        var lines = new List<string> { "Strategy,Symbol,EntryPrice,ExitPrice,Quantity,PnL,PnLPercent,EntryTime,ExitTime,EntryReason,ExitReason" };
        foreach (var runner in _runners)
            foreach (var t in runner.Portfolio.CompletedTrades)
                lines.Add($"{CsvField(runner.Strategy.Name)},{CsvField(t.Symbol)},{t.EntryPrice:F8},{t.ExitPrice:F8},{t.Quantity:F8},{t.PnL:F2},{t.PnLPercent:F2},{t.EntryTime:O},{t.ExitTime:O},{CsvField(t.EntryReason)},{CsvField(t.ExitReason)}");
        File.WriteAllLines(dlg.FileName, lines);
        AppendLog("INFO", $"Exported {lines.Count - 1} trades to {dlg.FileName}");
    }

    private void AppendLog(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}\n";
        if (InvokeRequired)
            Invoke(() => AppendLogDirect(level, line));
        else
            AppendLogDirect(level, line);
    }

    private void AppendLogDirect(string level, string line)
    {
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionLength = 0;
        _logBox.SelectionColor = level == "ERROR" ? Color.Red : level == "WARN" ? Color.Yellow : Color.LightGreen;
        _logBox.AppendText(line);
        _logBox.ScrollToCaret();
    }
}
