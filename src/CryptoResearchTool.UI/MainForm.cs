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
    private readonly HistoricalBacktestEngine _backtestEngine;
    private readonly ILogger<MainForm> _logger;

    private readonly List<StrategyRunner> _runners = new();
    private CancellationTokenSource? _cts;
    private System.Windows.Forms.Timer? _refreshTimer;
    private DateTime _startTime;
    private string _runSessionId = "";
    private AppMode _currentMode = AppMode.Live;

    // Top controls
    private DataGridView _gridStrategies = null!;
    private RichTextBox _logBox = null!;
    private Label _lblStatus = null!;
    private Label _lblUptime = null!;
    private Label _lblRunName = null!;
    private Button _btnStart = null!;
    private Button _btnStop = null!;
    private Button _btnOpenFolder = null!;
    private Button _btnExportCsv = null!;
    private Button _btnExportEquity = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _tsslConnection = null!;
    private ToolStripStatusLabel _tsslMode = null!;

    // Mode selector controls
    private RadioButton _rbLive = null!;
    private RadioButton _rbHistorical = null!;

    // Historical config panel controls
    private Panel _historicalConfigPanel = null!;
    private ComboBox _cmbSymbol = null!;
    private ComboBox _cmbTimeframe = null!;
    private DateTimePicker _dtpStartDate = null!;
    private DateTimePicker _dtpEndDate = null!;
    private NumericUpDown _nudInitialCapital = null!;
    private NumericUpDown _nudFeePercent = null!;
    private NumericUpDown _nudSlippagePercent = null!;
    private CheckBox _chkUseCache = null!;
    private CheckBox _chkForceRefresh = null!;
    // Historical test mode selector
    private RadioButton _rbGlobalTest = null!;
    private RadioButton _rbPerStrategyTest = null!;

    // Progress controls
    private Panel _progressPanel = null!;
    private ProgressBar _progressBar = null!;
    private Label _lblProgressStatus = null!;

    public MainForm(AppConfiguration config, IMarketDataProvider marketData,
        IPersistenceRepository repository, IMetricsCalculator metricsCalculator,
        HistoricalBacktestEngine backtestEngine,
        ILogger<MainForm> logger)
    {
        _config = config;
        _marketData = marketData;
        _repository = repository;
        _metricsCalculator = metricsCalculator;
        _backtestEngine = backtestEngine;
        _logger = logger;
        InitializeComponent();
        WireEvents();
        ApplyMode(AppMode.Live);
    }

    private void InitializeComponent()
    {
        Text = "Crypto Research Tool - Simulation Mode";
        Size = new Size(1280, 820);
        MinimumSize = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterScreen;

        // ── Top panel: action buttons + labels ─────────────────────────────
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(8) };
        _btnStart = new Button { Text = "▶ Start", Width = 100, Height = 36, Left = 8, Top = 12, BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnStop = new Button { Text = "■ Stop", Width = 100, Height = 36, Left = 116, Top = 12, BackColor = Color.FromArgb(220, 53, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };
        _btnOpenFolder = new Button { Text = "📁 Folder", Width = 100, Height = 36, Left = 224, Top = 12, FlatStyle = FlatStyle.Flat };
        _btnExportCsv = new Button { Text = "📊 Trades CSV", Width = 120, Height = 36, Left = 332, Top = 12, FlatStyle = FlatStyle.Flat };
        _btnExportEquity = new Button { Text = "📈 Equity CSV", Width = 120, Height = 36, Left = 460, Top = 12, FlatStyle = FlatStyle.Flat };
        _lblRunName = new Label { Text = "Run: -", AutoSize = true, Left = 594, Top = 20, Font = new Font("Segoe UI", 9f) };
        _lblUptime = new Label { Text = "Uptime: 00:00:00", AutoSize = true, Left = 788, Top = 20, Font = new Font("Segoe UI", 9f) };
        _lblStatus = new Label { Text = "", AutoSize = true, Left = 988, Top = 20, Font = new Font("Segoe UI", 9f) };
        topPanel.Controls.AddRange(new Control[] { _btnStart, _btnStop, _btnOpenFolder, _btnExportCsv, _btnExportEquity, _lblRunName, _lblUptime, _lblStatus });

        // ── Mode selector panel ─────────────────────────────────────────────
        var modePanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 6, 8, 6), BackColor = Color.FromArgb(248, 249, 250) };
        var modeGroup = new GroupBox { Text = "Mode", Left = 4, Top = 4, Width = 220, Height = 34, Font = new Font("Segoe UI", 8.5f) };
        _rbLive = new RadioButton { Text = "Live", Left = 10, Top = 12, Width = 60, Checked = true, Font = new Font("Segoe UI", 9f) };
        _rbHistorical = new RadioButton { Text = "Historical Analysis", Left = 74, Top = 12, Width = 140, Font = new Font("Segoe UI", 9f) };
        modeGroup.Controls.Add(_rbLive);
        modeGroup.Controls.Add(_rbHistorical);
        modePanel.Controls.Add(modeGroup);

        // ── Historical config panel (two rows + test mode row) ──────────────
        _historicalConfigPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 114,
            Padding = new Padding(8, 4, 8, 4),
            BackColor = Color.FromArgb(235, 245, 255),
            Visible = false
        };

        int lx = 8, cx = 70, spacing = 168;

        // Row 1: Symbol, Timeframe, Start Date, End Date
        AddLabelControl(_historicalConfigPanel, "Symbol:", lx, 8);
        _cmbSymbol = new ComboBox { Left = cx, Top = 6, Width = 110, DropDownStyle = ComboBoxStyle.DropDown };
        _cmbSymbol.Items.AddRange(new object[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT", "XRPUSDT", "ADAUSDT", "DOGEUSDT" });
        _cmbSymbol.Text = _config.Historical.Symbol;

        AddLabelControl(_historicalConfigPanel, "Timeframe:", lx + spacing, 8);
        _cmbTimeframe = new ComboBox { Left = cx + spacing, Top = 6, Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbTimeframe.Items.AddRange(new object[] { "1m", "5m", "15m", "30m", "1h", "2h", "4h", "1d" });
        _cmbTimeframe.SelectedItem = _config.Historical.Timeframe;
        if (_cmbTimeframe.SelectedIndex < 0) _cmbTimeframe.SelectedIndex = 4; // default 1h

        AddLabelControl(_historicalConfigPanel, "Start:", lx + spacing * 2, 8);
        _dtpStartDate = new DateTimePicker { Left = cx + spacing * 2, Top = 6, Width = 130, Format = DateTimePickerFormat.Short, Value = _config.Historical.StartDate };

        AddLabelControl(_historicalConfigPanel, "End:", lx + spacing * 3, 8);
        _dtpEndDate = new DateTimePicker { Left = cx + spacing * 3, Top = 6, Width = 130, Format = DateTimePickerFormat.Short, Value = _config.Historical.EndDate };

        // Row 2: Capital, Fee, Slippage, Checkboxes
        AddLabelControl(_historicalConfigPanel, "Capital ($):", lx, 44);
        _nudInitialCapital = new NumericUpDown { Left = cx, Top = 42, Width = 110, Minimum = 1, Maximum = 10_000_000, DecimalPlaces = 2, Value = (decimal)_config.Historical.InitialCapital, Increment = 100 };

        AddLabelControl(_historicalConfigPanel, "Fee %:", lx + spacing, 44);
        _nudFeePercent = new NumericUpDown { Left = cx + spacing, Top = 42, Width = 80, Minimum = 0, Maximum = 5, DecimalPlaces = 3, Value = (decimal)_config.Historical.FeePercent, Increment = 0.01m };

        AddLabelControl(_historicalConfigPanel, "Slippage %:", lx + spacing * 2, 44);
        _nudSlippagePercent = new NumericUpDown { Left = cx + spacing * 2, Top = 42, Width = 80, Minimum = 0, Maximum = 5, DecimalPlaces = 3, Value = (decimal)_config.Historical.SlippagePercent, Increment = 0.01m };

        _chkUseCache = new CheckBox { Text = "Use local cache", Left = lx + spacing * 3, Top = 44, Width = 130, Checked = _config.Historical.UseLocalCache };
        _chkForceRefresh = new CheckBox { Text = "Force refresh", Left = lx + spacing * 3 + 136, Top = 44, Width = 120, Checked = _config.Historical.ForceRefresh };

        // Row 3: Test mode selector
        var testModeLabel = new Label
        {
            Text = "Test mode:",
            Left = lx, Top = 82, AutoSize = true, Font = new Font("Segoe UI", 8.5f)
        };
        _rbGlobalTest = new RadioButton
        {
            Text = "Global test (same symbol/TF for all strategies)",
            Left = cx, Top = 80, Width = 320, Checked = true,
            Font = new Font("Segoe UI", 8.5f)
        };
        _rbPerStrategyTest = new RadioButton
        {
            Text = "Per-strategy test (each strategy uses its own symbol/TF)",
            Left = cx + 330, Top = 80, Width = 380,
            Font = new Font("Segoe UI", 8.5f)
        };

        _historicalConfigPanel.Controls.AddRange(new Control[]
        {
            _cmbSymbol, _cmbTimeframe, _dtpStartDate, _dtpEndDate,
            _nudInitialCapital, _nudFeePercent, _nudSlippagePercent,
            _chkUseCache, _chkForceRefresh,
            testModeLabel, _rbGlobalTest, _rbPerStrategyTest
        });

        // ── Progress panel ──────────────────────────────────────────────────
        _progressPanel = new Panel { Dock = DockStyle.Top, Height = 28, Padding = new Padding(8, 4, 8, 2), BackColor = Color.FromArgb(240, 240, 240), Visible = false };
        _progressBar = new ProgressBar { Left = 8, Top = 4, Width = 400, Height = 18, Style = ProgressBarStyle.Continuous };
        _lblProgressStatus = new Label { Left = 420, Top = 6, AutoSize = true, Font = new Font("Segoe UI", 8.5f), Text = "Idle" };
        _progressPanel.Controls.Add(_progressBar);
        _progressPanel.Controls.Add(_lblProgressStatus);

        // ── Strategy grid ───────────────────────────────────────────────────
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

        // ── Status strip ────────────────────────────────────────────────────
        _statusStrip = new StatusStrip();
        _tsslConnection = new ToolStripStatusLabel("Disconnected") { ForeColor = Color.Red };
        _tsslMode = new ToolStripStatusLabel("Mode: Live") { ForeColor = Color.DarkBlue };
        _statusStrip.Items.Add(_tsslConnection);
        _statusStrip.Items.Add(new ToolStripStatusLabel("|"));
        _statusStrip.Items.Add(_tsslMode);
        _statusStrip.Items.Add(new ToolStripStatusLabel("|"));
        _statusStrip.Items.Add(new ToolStripStatusLabel("Crypto Research Tool v2.0 - Simulation Mode"));

        // Panel order: bottom to top because DockStyle.Top stacks in reverse
        Controls.Add(split);
        Controls.Add(_progressPanel);
        Controls.Add(_historicalConfigPanel);
        Controls.Add(modePanel);
        Controls.Add(topPanel);
        Controls.Add(_statusStrip);
    }

    private static void AddLabelControl(Panel panel, string text, int left, int top)
    {
        panel.Controls.Add(new Label
        {
            Text = text,
            Left = left,
            Top = top + 2,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f)
        });
    }

    private void WireEvents()
    {
        _btnStart.Click += async (s, e) => await StartAsync();
        _btnStop.Click += async (s, e) => await StopAsync();
        _btnOpenFolder.Click += (s, e) => OpenDataFolder();
        _btnExportCsv.Click += (s, e) => ExportTradesCsv();
        _btnExportEquity.Click += (s, e) => ExportEquityCsv();
        _gridStrategies.CellDoubleClick += (s, e) => ShowStrategyDetail(e.RowIndex);
        _marketData.ConnectionChanged += (s, e) => Invoke(() => UpdateConnectionStatus(e));
        FormClosing += async (s, e) => { if (_cts != null) await StopAsync(); };

        _rbLive.CheckedChanged += (s, e) => { if (_rbLive.Checked) ApplyMode(AppMode.Live); };
        _rbHistorical.CheckedChanged += (s, e) => { if (_rbHistorical.Checked) ApplyMode(AppMode.Historical); };

        _chkForceRefresh.CheckedChanged += (s, e) =>
        {
            if (_chkForceRefresh.Checked) _chkUseCache.Checked = false;
        };
        _chkUseCache.CheckedChanged += (s, e) =>
        {
            if (_chkUseCache.Checked) _chkForceRefresh.Checked = false;
        };
    }

    private void ApplyMode(AppMode mode)
    {
        _currentMode = mode;
        _historicalConfigPanel.Visible = (mode == AppMode.Historical);
        _progressPanel.Visible = (mode == AppMode.Historical);
        _tsslMode.Text = mode == AppMode.Live ? "Mode: Live" : "Mode: Historical Analysis";
        _tsslMode.ForeColor = mode == AppMode.Live ? Color.DarkBlue : Color.DarkGreen;
    }

    private async Task StartAsync()
    {
        if (_currentMode == AppMode.Historical)
            await StartHistoricalAsync();
        else
            await StartLiveAsync();
    }

    // ── LIVE MODE ──────────────────────────────────────────────────────────

    private async Task StartLiveAsync()
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
                    stratConfig,
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

            AppendLog("INFO", $"[LIVE] Started run: {_config.RunName} with {_runners.Count} strategies");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live start failed");
            AppendLog("ERROR", $"Start failed: {ex.Message}");
            _btnStart.Enabled = true;
            _btnStop.Enabled = false;
        }
    }

    // ── HISTORICAL MODE ────────────────────────────────────────────────────

    private async Task StartHistoricalAsync()
    {
        if (!ValidateHistoricalConfig(out var errorMsg))
        {
            MessageBox.Show(errorMsg, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _cts = new CancellationTokenSource();
            _startTime = DateTime.UtcNow;

            var historicalConfig = BuildHistoricalConfig();

            await _repository.InitializeAsync();
            var runName = $"Historical_{historicalConfig.Symbol}_{historicalConfig.Timeframe}_{DateTime.Now:yyyyMMdd_HHmmss}";
            _runSessionId = await _repository.CreateRunSessionAsync(runName, "{}");
            _lblRunName.Text = $"Run: {runName}";

            _runners.Clear();
            _gridStrategies.Rows.Clear();
            _progressBar.Value = 0;
            _lblProgressStatus.Text = "Starting...";

            AppendLog("INFO", $"[HISTORICAL] Starting backtest: {historicalConfig.Symbol} {historicalConfig.Timeframe} " +
                               $"{historicalConfig.StartDate:yyyy-MM-dd} → {historicalConfig.EndDate:yyyy-MM-dd} " +
                               $"[{historicalConfig.TestMode}]");

            var progress = new Progress<(string status, int candlesProcessed, int totalCandles)>(p =>
            {
                if (InvokeRequired)
                    Invoke(() => UpdateHistoricalProgress(p.status, p.candlesProcessed, p.totalCandles));
                else
                    UpdateHistoricalProgress(p.status, p.candlesProcessed, p.totalCandles);
            });

            List<StrategyRunner> runners;
            try
            {
                runners = await _backtestEngine.RunAsync(
                    historicalConfig,
                    _config.Strategies,
                    _runSessionId,
                    progress,
                    _cts.Token);
            }
            catch (OperationCanceledException)
            {
                AppendLog("WARN", "Historical analysis cancelled.");
                runners = new List<StrategyRunner>();
            }

            _runners.AddRange(runners);

            // Populate grid with results
            foreach (var runner in _runners)
            {
                var state = runner.Strategy.GetState();
                _gridStrategies.Rows.Add(
                    runner.Strategy.Name,
                    runner.Strategy.Symbol,
                    runner.Strategy.Timeframe,
                    "-", "-", "0", "-", "-", "-", "-", "-", "Completed");
            }

            if (_runners.Count > 0)
            {
                UpdateGrid();
                // Log per-strategy summary
                foreach (var runner in _runners)
                {
                    var m = runner.CurrentMetrics;
                    AppendLog("INFO",
                        $"  [{m.StrategyName}] Return={m.ReturnPercent:F2}% Trades={m.TotalTrades} " +
                        $"WinRate={m.WinRate:F1}% DD={m.MaxDrawdownPercent:F2}% Sharpe={m.SharpeRatio:F2}");
                }
                AppendLog("INFO", $"[HISTORICAL] Analysis complete. {_runners.Count} strategies evaluated.");
                UpdateHistoricalProgress("Completed", 1, 1);
            }
            else
            {
                AppendLog("WARN", "[HISTORICAL] No results - check strategy configuration.");
                UpdateHistoricalProgress("No data returned.", 0, 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Historical analysis failed");
            AppendLog("ERROR", $"Historical analysis failed: {ex.Message}");
            UpdateHistoricalProgress("Failed: " + ex.Message, 0, 0);
        }
        finally
        {
            _btnStart.Enabled = true;
            _btnStop.Enabled = false;
        }
    }

    private void UpdateHistoricalProgress(string status, int processed, int total)
    {
        _lblProgressStatus.Text = status;
        if (total > 0)
        {
            _progressBar.Maximum = total;
            _progressBar.Value = Math.Min(processed, total);
        }
        else
        {
            _progressBar.Style = ProgressBarStyle.Marquee;
        }

        if (status.StartsWith("Downloading") || status.StartsWith("Using local cache"))
            _progressBar.Style = ProgressBarStyle.Marquee;
        else if (total > 0)
            _progressBar.Style = ProgressBarStyle.Continuous;
    }

    private bool ValidateHistoricalConfig(out string errorMsg)
    {
        errorMsg = "";
        if (string.IsNullOrWhiteSpace(_cmbSymbol.Text))
        { errorMsg = "Symbol cannot be empty."; return false; }
        if (_cmbTimeframe.SelectedItem == null)
        { errorMsg = "Please select a timeframe."; return false; }
        if (_dtpEndDate.Value.Date <= _dtpStartDate.Value.Date)
        { errorMsg = "End date must be after start date."; return false; }
        if ((decimal)_nudInitialCapital.Value <= 0)
        { errorMsg = "Initial capital must be greater than 0."; return false; }
        if (!_config.Strategies.Any(s => s.Enabled))
        { errorMsg = "No enabled strategies found. Check strategies.json."; return false; }
        return true;
    }

    private HistoricalAnalysisConfiguration BuildHistoricalConfig() => new()
    {
        Symbol = _cmbSymbol.Text.Trim().ToUpperInvariant(),
        Timeframe = _cmbTimeframe.SelectedItem!.ToString()!,
        StartDate = _dtpStartDate.Value.ToUniversalTime().Date,
        EndDate = _dtpEndDate.Value.ToUniversalTime().Date.AddDays(1),
        InitialCapital = _nudInitialCapital.Value,
        FeePercent = _nudFeePercent.Value,
        SlippagePercent = _nudSlippagePercent.Value,
        UseLocalCache = _chkUseCache.Checked,
        ForceRefresh = _chkForceRefresh.Checked,
        CacheDirectory = _config.Historical.CacheDirectory,
        TestMode = _rbPerStrategyTest.Checked ? HistoricalTestMode.PerStrategy : HistoricalTestMode.Global
    };

    // ── STOP ───────────────────────────────────────────────────────────────

    private async Task StopAsync()
    {
        _refreshTimer?.Stop();
        _cts?.Cancel();

        if (_currentMode == AppMode.Live)
        {
            _marketData.TickReceived -= OnTickReceived;
            _marketData.CandleReceived -= OnCandleReceived;
            await _marketData.DisconnectAsync();
            foreach (var runner in _runners)
                await runner.UpdateMetricsAsync();
        }

        _btnStart.Enabled = true;
        _btnStop.Enabled = false;
        AppendLog("INFO", "Stopped.");
    }

    // ── LIVE event handlers ────────────────────────────────────────────────

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

    // ── GRID UPDATE ────────────────────────────────────────────────────────

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

            if (_currentMode == AppMode.Historical)
            {
                row.Cells["colSignal"].Value = state.LastSignal != null
                    ? $"{state.LastSignal.Type} @ {state.LastSignal.Price:F2}"
                    : "-";
                row.Cells["colStatus"].Value = "Completed";
            }
            else
            {
                row.Cells["colSignal"].Value = state.LastSignal != null
                    ? $"{state.LastSignal.Type} @ {state.LastSignal.Price:F2}"
                    : "-";
                row.Cells["colStatus"].Value = state.OpenPosition != null
                    ? $"OPEN {state.OpenPosition.Symbol} UPnL:{state.OpenPosition.UnrealizedPnL:F2}"
                    : "No Position";
            }

            row.DefaultCellStyle.BackColor = m.NetProfit >= 0
                ? Color.FromArgb(230, 255, 230)
                : Color.FromArgb(255, 230, 230);
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

    private void ExportTradesCsv()
    {
        if (!_runners.Any())
        {
            MessageBox.Show("No strategies have run yet.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"trades_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        static string CsvField(string s) => s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;

        var lines = new List<string>
        {
            "Strategy,Symbol,EntryPrice,ExitPrice,Quantity,GrossPnL,NetPnL,PnLPercent,TotalFees,SlippageImpact,EntryTime,ExitTime,HoldingHours,EntryReason,ExitReason,ExitReasonCategory"
        };
        foreach (var runner in _runners)
            foreach (var t in runner.Portfolio.CompletedTrades)
                lines.Add(string.Join(",",
                    CsvField(runner.Strategy.Name),
                    CsvField(t.Symbol),
                    t.EntryPrice.ToString("F8"),
                    t.ExitPrice.ToString("F8"),
                    t.Quantity.ToString("F8"),
                    t.GrossPnL.ToString("F2"),
                    t.PnL.ToString("F2"),
                    t.PnLPercent.ToString("F2"),
                    t.TotalFees.ToString("F4"),
                    t.SlippageImpact.ToString("F4"),
                    t.EntryTime.ToString("O"),
                    t.ExitTime.ToString("O"),
                    t.HoldingTime.TotalHours.ToString("F2"),
                    CsvField(t.EntryReason),
                    CsvField(t.ExitReason),
                    CsvField(t.ExitReasonCategory)));

        File.WriteAllLines(dlg.FileName, lines);
        AppendLog("INFO", $"Exported {lines.Count - 1} trades to {dlg.FileName}");
    }

    private void ExportEquityCsv()
    {
        if (!_runners.Any())
        {
            MessageBox.Show("No strategies have run yet.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"equity_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        static string CsvField(string s) => s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;

        var lines = new List<string> { "Strategy,Timestamp,Equity,Cash,UnrealizedPnL" };
        foreach (var runner in _runners)
            foreach (var ep in runner.EquityHistory)
                lines.Add(string.Join(",",
                    CsvField(runner.Strategy.Name),
                    ep.Timestamp.ToString("O"),
                    ep.Equity.ToString("F2"),
                    ep.Cash.ToString("F2"),
                    ep.UnrealizedPnL.ToString("F2")));

        File.WriteAllLines(dlg.FileName, lines);
        AppendLog("INFO", $"Exported equity curve ({lines.Count - 1} points) to {dlg.FileName}");
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
