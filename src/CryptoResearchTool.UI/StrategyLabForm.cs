using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Application.Optimization;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Optimization;

namespace CryptoResearchTool.UI;

/// <summary>
/// Strategy Lab – a simplified, unified form for strategy backtesting and parameter
/// optimization. The user picks a strategy type, configures a small set of parameters,
/// runs a single backtest, and can optionally optimize all parameters with one click.
///
/// Workflow:
///   1. Select symbol / timeframe / strategy type / date range
///   2. Adjust manual parameters (pre-filled with sensible defaults)
///   3. Run Backtest – see results immediately
///   4. Optimize Parameters – random-search optimization with predefined ranges
///   5. Apply Best Result – copies optimized parameters back to the preset
///   6. Export Results – CSV export
/// </summary>
public class StrategyLabForm : Form
{
    // ── Dependencies ─────────────────────────────────────────────────────────
    private readonly IOptimizationEngine _engine;
    private readonly AppConfiguration    _appConfig;

    // ── Current StrategyPreset state ─────────────────────────────────────────
    private string _presetType = "MovingAverageCrossover";
    private Dictionary<string, decimal> _presetParams = new();

    // ── UI: top controls ─────────────────────────────────────────────────────
    private ComboBox       _cmbSymbol     = null!;
    private ComboBox       _cmbTimeframe  = null!;
    private ComboBox       _cmbStrategy   = null!;
    private DateTimePicker _dtpStart      = null!;
    private DateTimePicker _dtpEnd        = null!;
    private NumericUpDown  _nudCapital    = null!;
    private NumericUpDown  _nudFee        = null!;
    private NumericUpDown  _nudSlippage   = null!;

    // ── UI: parameter panel ──────────────────────────────────────────────────
    private Panel _paramPanel = null!;
    private readonly Dictionary<string, NumericUpDown> _paramControls = new();

    // ── UI: action buttons ───────────────────────────────────────────────────
    private Button _btnRunBacktest  = null!;
    private Button _btnOptimize     = null!;
    private Button _btnApply        = null!;
    private Button _btnExportCsv    = null!;
    private Button _btnCancel       = null!;

    // ── UI: progress ─────────────────────────────────────────────────────────
    private ProgressBar _progressBar = null!;
    private Label       _lblProgress = null!;
    private Label       _lblElapsed  = null!;

    // ── UI: single backtest result ───────────────────────────────────────────
    private Label _lblBacktestResult = null!;

    // ── UI: optimization results grid ────────────────────────────────────────
    private DataGridView _gridResults   = null!;
    private Label        _lblResultInfo = null!;

    // ── Runtime state ────────────────────────────────────────────────────────
    private CancellationTokenSource?    _cts;
    private List<OptimizationResult>    _optimizationResults = new();
    private System.Windows.Forms.Timer? _elapsedTimer;
    private DateTime                    _runStart;

    // ── Constructor ──────────────────────────────────────────────────────────

    public StrategyLabForm(IOptimizationEngine engine, AppConfiguration appConfig)
    {
        _engine    = engine;
        _appConfig = appConfig;

        InitializeComponent();
        LoadDefaults();
    }

    // ── Form layout ──────────────────────────────────────────────────────────

    private void InitializeComponent()
    {
        Text          = "Strategy Lab";
        Size          = new Size(1300, 850);
        MinimumSize   = new Size(1000, 700);
        StartPosition = FormStartPosition.CenterParent;
        BackColor     = Color.FromArgb(240, 242, 245);

        var outerSplit = new SplitContainer
        {
            Dock        = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            BackColor   = Color.FromArgb(240, 242, 245),
        };

        outerSplit.Panel1.Controls.Add(BuildTopPanel());
        outerSplit.Panel2.Controls.Add(BuildBottomPanel());

        Controls.Add(outerSplit);

        Load += (s, e) =>
        {
            outerSplit.Panel1MinSize   = 280;
            outerSplit.Panel2MinSize   = 250;
            outerSplit.SplitterDistance = 360;
        };
    }

    // ── Top panel: config + parameters + actions ─────────────────────────────

    private Panel BuildTopPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), AutoScroll = true };

        int y  = 4;
        int lw = 90;   // label width
        int cw = 140;  // control width
        int col2 = 260; // second column start

        // ── Section: Strategy & Market ───────────────────────────────────────
        panel.Controls.Add(SectionLabel("Strategy & Market", 0, y)); y += 22;

        panel.Controls.Add(MakeLabel("Symbol:", 0, y));
        _cmbSymbol = new ComboBox { Left = lw, Top = y, Width = cw, DropDownStyle = ComboBoxStyle.DropDown };
        _cmbSymbol.Items.AddRange(new object[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT", "XRPUSDT", "ADAUSDT", "DOGEUSDT" });
        _cmbSymbol.Text = _appConfig.Historical.Symbol;
        panel.Controls.Add(_cmbSymbol);

        panel.Controls.Add(MakeLabel("Timeframe:", col2, y));
        _cmbTimeframe = new ComboBox { Left = col2 + lw, Top = y, Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbTimeframe.Items.AddRange(new object[] { "1m", "5m", "15m", "30m", "1h", "2h", "4h", "1d" });
        _cmbTimeframe.SelectedItem = _appConfig.Historical.Timeframe;
        if (_cmbTimeframe.SelectedIndex < 0) _cmbTimeframe.SelectedIndex = 4;
        panel.Controls.Add(_cmbTimeframe);
        y += 28;

        panel.Controls.Add(MakeLabel("Strategy:", 0, y));
        _cmbStrategy = new ComboBox { Left = lw, Top = y, Width = cw + 50, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var type in StrategyParameterDescriptorRegistry.KnownStrategyTypes)
            _cmbStrategy.Items.Add(type);
        _cmbStrategy.SelectedIndexChanged += OnStrategyTypeChanged;
        panel.Controls.Add(_cmbStrategy);
        y += 28;

        panel.Controls.Add(MakeLabel("Start:", 0, y));
        _dtpStart = new DateTimePicker { Left = lw, Top = y, Width = cw, Format = DateTimePickerFormat.Short, Value = _appConfig.Historical.StartDate };
        panel.Controls.Add(_dtpStart);

        panel.Controls.Add(MakeLabel("End:", col2, y));
        _dtpEnd = new DateTimePicker { Left = col2 + lw, Top = y, Width = cw, Format = DateTimePickerFormat.Short, Value = _appConfig.Historical.EndDate };
        panel.Controls.Add(_dtpEnd);
        y += 28;

        panel.Controls.Add(MakeLabel("Capital ($):", 0, y));
        _nudCapital = new NumericUpDown { Left = lw, Top = y, Width = 100, Minimum = 1, Maximum = 10_000_000, DecimalPlaces = 2, Value = _appConfig.Historical.InitialCapital, Increment = 100 };
        panel.Controls.Add(_nudCapital);

        panel.Controls.Add(MakeLabel("Fee %:", col2, y));
        _nudFee = new NumericUpDown { Left = col2 + lw, Top = y, Width = 80, Minimum = 0, Maximum = 5, DecimalPlaces = 3, Value = _appConfig.Historical.FeePercent, Increment = 0.01m };
        panel.Controls.Add(_nudFee);

        int col3 = col2 + 200;
        panel.Controls.Add(MakeLabel("Slippage %:", col3, y));
        _nudSlippage = new NumericUpDown { Left = col3 + lw, Top = y, Width = 80, Minimum = 0, Maximum = 5, DecimalPlaces = 3, Value = _appConfig.Historical.SlippagePercent, Increment = 0.01m };
        panel.Controls.Add(_nudSlippage);
        y += 34;

        // ── Section: Parameters (dynamic) ────────────────────────────────────
        panel.Controls.Add(SectionLabel("Strategy Parameters", 0, y)); y += 22;

        _paramPanel = new Panel { Left = 0, Top = y, Width = 700, Height = 10, AutoSize = true };
        panel.Controls.Add(_paramPanel);

        // ── Section: Actions ─────────────────────────────────────────────────
        var actionPanel = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(4) };

        _btnRunBacktest = new Button
        {
            Text = "▶ Run Backtest", Left = 4, Top = 8, Width = 140, Height = 34,
            BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        _btnOptimize = new Button
        {
            Text = "⚙ Optimize Parameters", Left = 152, Top = 8, Width = 170, Height = 34,
            BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        _btnCancel = new Button
        {
            Text = "■ Cancel", Left = 330, Top = 8, Width = 90, Height = 34,
            BackColor = Color.FromArgb(220, 53, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false
        };

        _lblBacktestResult = new Label
        {
            Left = 4, Top = 48, AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 102, 153)
        };

        _progressBar = new ProgressBar { Left = 440, Top = 12, Width = 300, Height = 18, Style = ProgressBarStyle.Continuous };
        _lblProgress = new Label { Left = 750, Top = 14, AutoSize = true };
        _lblElapsed  = new Label { Left = 440, Top = 34, AutoSize = true, ForeColor = Color.DimGray };

        _btnRunBacktest.Click += OnRunBacktestClicked;
        _btnOptimize.Click    += OnOptimizeClicked;
        _btnCancel.Click      += (_, _) => _cts?.Cancel();

        actionPanel.Controls.AddRange(new Control[]
        {
            _btnRunBacktest, _btnOptimize, _btnCancel,
            _progressBar, _lblProgress, _lblElapsed, _lblBacktestResult
        });
        panel.Controls.Add(actionPanel);

        return panel;
    }

    // ── Bottom panel: results ────────────────────────────────────────────────

    private Panel BuildBottomPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };

        // Header with action buttons
        var headerPanel = new Panel { Dock = DockStyle.Top, Height = 30 };

        _lblResultInfo = new Label
        {
            Left = 0, Top = 6, AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 102, 153)
        };

        _btnApply = new Button
        {
            Text = "✔ Apply Selected Result", Left = 0, Top = 2, Width = 180, Height = 26,
            BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Enabled = false
        };
        _btnExportCsv = new Button
        {
            Text = "📊 Export CSV", Left = 0, Top = 2, Width = 110, Height = 26,
            FlatStyle = FlatStyle.Flat, Enabled = false
        };

        headerPanel.SizeChanged += (_, _) =>
        {
            _btnApply.Left     = headerPanel.Width - 190;
            _btnExportCsv.Left = headerPanel.Width - 190 - 120;
        };

        _btnApply.Click     += OnApplyClicked;
        _btnExportCsv.Click += OnExportCsvClicked;

        headerPanel.Controls.AddRange(new Control[] { _lblResultInfo, _btnApply, _btnExportCsv });
        panel.Controls.Add(headerPanel);

        // Results grid
        _gridResults = new DataGridView
        {
            Dock                  = DockStyle.Fill,
            AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            ReadOnly              = true,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor       = Color.White,
            RowHeadersVisible     = false,
            MultiSelect           = false,
        };
        BuildResultsColumns();
        _gridResults.SelectionChanged += (_, _) =>
            _btnApply.Enabled = _gridResults.SelectedRows.Count > 0;

        panel.Controls.Add(_gridResults);
        return panel;
    }

    private void BuildResultsColumns()
    {
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRank",      HeaderText = "Rank",         FillWeight = 5  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colScore",     HeaderText = "Score",        FillWeight = 8  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTrRet",     HeaderText = "Train Ret%",   FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTrDD",      HeaderText = "Train DD%",    FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTrSharpe",  HeaderText = "Train Sharpe", FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTrPF",      HeaderText = "Train PF",     FillWeight = 7  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTrTrades",  HeaderText = "Train Trades", FillWeight = 7  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValRet",    HeaderText = "Val Ret%",     FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValDD",     HeaderText = "Val DD%",      FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValSharpe", HeaderText = "Val Sharpe",   FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValPF",     HeaderText = "Val PF",       FillWeight = 7  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValTrades", HeaderText = "Val Trades",   FillWeight = 7  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colParams",    HeaderText = "Parameters",   FillWeight = 45 });
    }

    // ── Load defaults ────────────────────────────────────────────────────────

    private void LoadDefaults()
    {
        if (_cmbStrategy.Items.Count > 0)
            _cmbStrategy.SelectedIndex = 0;
    }

    // ── Strategy type changed → rebuild parameter panel ──────────────────────

    private void OnStrategyTypeChanged(object? sender, EventArgs e)
    {
        _presetType = _cmbStrategy.SelectedItem?.ToString() ?? "MovingAverageCrossover";
        _presetParams = StrategyParameterDescriptorRegistry.GetDefaultValues(_presetType);
        RebuildParamPanel();
    }

    private void RebuildParamPanel()
    {
        _paramPanel.Controls.Clear();
        _paramControls.Clear();

        var descriptors = StrategyParameterDescriptorRegistry.GetDescriptors(_presetType);
        int y  = 0;
        int lw = 180;
        int cw = 100;
        int col2 = 300;
        int col = 0;

        foreach (var d in descriptors)
        {
            int xLabel = col == 0 ? 0 : col2;
            int xCtrl  = col == 0 ? lw : col2 + lw;

            _paramPanel.Controls.Add(MakeLabel(d.ParameterName + ":", xLabel, y));

            bool isInt = d.Type == ParameterType.Integer;
            decimal currentValue = _presetParams.TryGetValue(d.ParameterName, out var v) ? v
                : isInt ? Math.Round((d.DefaultMin + d.DefaultMax) / 2m, 0)
                : Math.Round((d.DefaultMin + d.DefaultMax) / 2m, 2);

            var nud = new NumericUpDown
            {
                Left          = xCtrl,
                Top           = y,
                Width         = cw,
                Minimum       = d.DefaultMin - (isInt ? 10 : 5m),
                Maximum       = d.DefaultMax + (isInt ? 50 : 10m),
                DecimalPlaces = isInt ? 0 : 2,
                Increment     = d.DefaultStep,
                Value         = Math.Max(d.DefaultMin - (isInt ? 10 : 5m), Math.Min(d.DefaultMax + (isInt ? 50 : 10m), currentValue)),
            };
            _paramPanel.Controls.Add(nud);
            _paramControls[d.ParameterName] = nud;

            if (col == 0) { col = 1; }
            else { col = 0; y += 28; }
        }
        if (col == 1) y += 28;

        _paramPanel.Height = y + 4;
    }

    // ── Collect current parameter values from UI ─────────────────────────────

    private Dictionary<string, decimal> CollectCurrentParams()
    {
        var result = new Dictionary<string, decimal>();
        foreach (var (name, nud) in _paramControls)
            result[name] = nud.Value;
        return result;
    }

    private StrategyConfiguration BuildPresetConfig()
    {
        var config = new StrategyConfiguration
        {
            Type      = _presetType,
            Name      = _presetType,
            Symbol    = _cmbSymbol.Text.Trim().ToUpperInvariant(),
            Timeframe = _cmbTimeframe.SelectedItem?.ToString() ?? "1h",
            Enabled   = true,
        };

        var paramValues = CollectCurrentParams();
        StrategyConfigurationCloner.ApplyParameters(config, paramValues);
        return config;
    }

    // ── Run Backtest ─────────────────────────────────────────────────────────

    private async void OnRunBacktestClicked(object? sender, EventArgs e)
    {
        if (!ValidateInputs()) return;

        SetRunning(true);
        _lblBacktestResult.Text = "Running backtest…";

        var config = BuildPresetConfig();
        var paramValues = CollectCurrentParams();

        // Build an optimization request with exactly 1 combination (the current params)
        var request = new OptimizationRequest
        {
            BaseStrategy         = config,
            Symbol               = config.Symbol,
            Timeframe            = config.Timeframe,
            StartDate            = _dtpStart.Value.Date,
            EndDate              = _dtpEnd.Value.Date.AddDays(1).AddSeconds(-1),
            InitialCapital       = _nudCapital.Value,
            FeePercent           = _nudFee.Value,
            SlippagePercent      = _nudSlippage.Value,
            SearchMode           = OptimizationSearchMode.RandomSearch,
            Objective            = OptimizationObjective.RobustScore,
            ParameterRanges      = BuildFixedRanges(paramValues),
            RandomSampleCount    = 1,
            EnableValidationSplit = true,
            TrainPercent         = 70m,
            UseLocalCache        = true,
        };

        _cts      = new CancellationTokenSource();
        _runStart = DateTime.UtcNow;
        StartElapsedTimer();

        var progress = new Progress<OptimizationProgress>(p =>
        {
            if (!IsHandleCreated || IsDisposed) return;
            _lblProgress.Text  = p.Status;
            int pct = p.Total > 0 ? (int)(p.CurrentIndex * 100L / p.Total) : 0;
            _progressBar.Value = Math.Clamp(pct, 0, 100);
        });

        try
        {
            var results = await Task.Run(
                () => _engine.RunAsync(request, progress, _cts.Token), _cts.Token);

            if (IsDisposed) return;

            if (results.Count > 0)
            {
                var r = results[0];
                var t = r.TrainMetrics;
                var v = r.ValidationMetrics;
                _lblBacktestResult.Text =
                    $"Train: Ret={t.ReturnPct:F2}%  DD={t.MaxDrawdownPct:F2}%  Sharpe={t.SharpeRatio:F3}  PF={t.ProfitFactor:F2}  Trades={t.Trades}" +
                    (v != null ? $"   |   Val: Ret={v.ReturnPct:F2}%  DD={v.MaxDrawdownPct:F2}%  Sharpe={v.SharpeRatio:F3}  PF={v.ProfitFactor:F2}  Trades={v.Trades}" : "");
                _lblBacktestResult.ForeColor = t.ReturnPct >= 0 ? Color.DarkGreen : Color.DarkRed;
            }
            else
            {
                _lblBacktestResult.Text = "No results returned.";
                _lblBacktestResult.ForeColor = Color.DarkRed;
            }
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed) _lblBacktestResult.Text = "Backtest cancelled.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                MessageBox.Show($"Backtest failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _lblBacktestResult.Text = "Error.";
            }
        }
        finally
        {
            if (!IsDisposed) SetRunning(false);
            try { _elapsedTimer?.Stop(); } catch (ObjectDisposedException) { }
        }
    }

    /// <summary>
    /// Builds parameter ranges where min == max so the engine evaluates exactly
    /// the supplied values (effectively a single-combination run).
    /// </summary>
    private static List<StrategyParameterRange> BuildFixedRanges(Dictionary<string, decimal> paramValues)
    {
        var list = new List<StrategyParameterRange>();
        foreach (var (name, value) in paramValues)
        {
            list.Add(new StrategyParameterRange
            {
                ParameterName = name,
                MinValue      = value,
                MaxValue      = value,
                Step          = 1m,
                IsEnabled     = true,
                IsInteger     = value == Math.Floor(value),
            });
        }
        return list;
    }

    // ── Optimize Parameters ──────────────────────────────────────────────────

    private async void OnOptimizeClicked(object? sender, EventArgs e)
    {
        if (!ValidateInputs()) return;

        SetRunning(true);
        _optimizationResults.Clear();
        _gridResults.Rows.Clear();
        _lblResultInfo.Text = "Running optimization…";
        _lblBacktestResult.Text = "";

        var config = BuildPresetConfig();

        // Build ranges from the registry defaults (all parameters enabled)
        var descriptors = StrategyParameterDescriptorRegistry.GetDescriptors(_presetType);
        var ranges = descriptors.Select(d => new StrategyParameterRange
        {
            ParameterName = d.ParameterName,
            MinValue      = d.DefaultMin,
            MaxValue      = d.DefaultMax,
            Step          = d.DefaultStep,
            IsEnabled     = true,
            IsInteger     = d.Type == ParameterType.Integer,
        }).ToList();

        var request = new OptimizationRequest
        {
            BaseStrategy          = config,
            Symbol                = _cmbSymbol.Text.Trim().ToUpperInvariant(),
            Timeframe             = _cmbTimeframe.SelectedItem?.ToString() ?? "1h",
            StartDate             = _dtpStart.Value.Date,
            EndDate               = _dtpEnd.Value.Date.AddDays(1).AddSeconds(-1),
            InitialCapital        = _nudCapital.Value,
            FeePercent            = _nudFee.Value,
            SlippagePercent       = _nudSlippage.Value,
            SearchMode            = OptimizationSearchMode.RandomSearch,
            Objective             = OptimizationObjective.RobustScore,
            ParameterRanges       = ranges,
            RandomSampleCount     = 200,
            EnableValidationSplit = true,
            TrainPercent          = 70m,
            UseLocalCache         = true,
        };

        _cts      = new CancellationTokenSource();
        _runStart = DateTime.UtcNow;
        StartElapsedTimer();

        var progress = new Progress<OptimizationProgress>(p =>
        {
            if (!IsHandleCreated || IsDisposed) return;
            _lblProgress.Text  = p.Status;
            int pct = p.Total > 0 ? (int)(p.CurrentIndex * 100L / p.Total) : 0;
            _progressBar.Value = Math.Clamp(pct, 0, 100);
        });

        try
        {
            _optimizationResults = await Task.Run(
                () => _engine.RunAsync(request, progress, _cts.Token), _cts.Token);

            if (IsDisposed) return;

            PopulateResultsGrid(_optimizationResults);
            _lblResultInfo.Text = $"{_optimizationResults.Count} results  (best score: {_optimizationResults.FirstOrDefault()?.OverallScore:F2})";
            _btnExportCsv.Enabled = _optimizationResults.Count > 0;
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed) _lblResultInfo.Text = "Optimization cancelled.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                MessageBox.Show($"Optimization failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _lblResultInfo.Text = "Error.";
            }
        }
        finally
        {
            if (!IsDisposed) SetRunning(false);
            try { _elapsedTimer?.Stop(); } catch (ObjectDisposedException) { }
        }
    }

    // ── Apply result ─────────────────────────────────────────────────────────

    private void OnApplyClicked(object? sender, EventArgs e)
    {
        if (_gridResults.SelectedRows.Count == 0) return;
        var result = _gridResults.SelectedRows[0].Tag as OptimizationResult;
        if (result == null) return;

        var ans = MessageBox.Show(
            $"Apply these optimized parameters to the current preset?\n\n{result.ParameterSummary}",
            "Apply Parameters", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (ans != DialogResult.Yes) return;

        // Update preset params and refresh UI controls
        _presetParams = new Dictionary<string, decimal>(result.ParameterValues);

        foreach (var (name, value) in result.ParameterValues)
        {
            if (_paramControls.TryGetValue(name, out var nud))
            {
                try
                {
                    decimal clamped = Math.Max(nud.Minimum, Math.Min(nud.Maximum, value));
                    nud.Value = clamped;
                }
                catch { }
            }
        }

        MessageBox.Show(
            "Parameters applied to the current preset.\n\n" +
            "Click 'Run Backtest' to verify the result, or close the lab.",
            "Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── Export CSV ────────────────────────────────────────────────────────────

    private void OnExportCsvClicked(object? sender, EventArgs e)
    {
        if (_optimizationResults.Count == 0) return;

        using var dlg = new SaveFileDialog
        {
            Filter   = "CSV|*.csv",
            FileName = $"strategylab_{_cmbSymbol.Text}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var rows = new List<string>
        {
            "Rank,Score,Train_RetPct,Train_MaxDDPct,Train_Sharpe,Train_PF,Train_Trades," +
            "Val_RetPct,Val_MaxDDPct,Val_Sharpe,Val_PF,Val_Trades,Parameters"
        };

        foreach (var r in _optimizationResults)
        {
            var t = r.TrainMetrics;
            var v = r.ValidationMetrics;
            rows.Add(string.Join(",",
                r.Rank,
                r.OverallScore.ToString("F4"),
                t.ReturnPct.ToString("F4"),
                t.MaxDrawdownPct.ToString("F4"),
                t.SharpeRatio.ToString("F4"),
                t.ProfitFactor.ToString("F4"),
                t.Trades,
                v != null ? v.ReturnPct.ToString("F4")      : "",
                v != null ? v.MaxDrawdownPct.ToString("F4") : "",
                v != null ? v.SharpeRatio.ToString("F4")    : "",
                v != null ? v.ProfitFactor.ToString("F4")   : "",
                v != null ? v.Trades.ToString()             : "",
                CsvField(r.ParameterSummary)));
        }

        File.WriteAllLines(dlg.FileName, rows);
        MessageBox.Show($"Exported {rows.Count - 1} rows to:\n{dlg.FileName}",
            "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── Results grid population ───────────────────────────────────────────────

    private void PopulateResultsGrid(List<OptimizationResult> results)
    {
        _gridResults.Rows.Clear();
        foreach (var r in results)
        {
            var t = r.TrainMetrics;
            var v = r.ValidationMetrics;
            int rowIdx = _gridResults.Rows.Add(
                r.Rank,
                r.OverallScore.ToString("F2"),
                t.ReturnPct.ToString("F2"),
                t.MaxDrawdownPct.ToString("F2"),
                t.SharpeRatio.ToString("F3"),
                t.ProfitFactor.ToString("F2"),
                t.Trades.ToString(),
                v != null ? v.ReturnPct.ToString("F2")      : "-",
                v != null ? v.MaxDrawdownPct.ToString("F2") : "-",
                v != null ? v.SharpeRatio.ToString("F3")    : "-",
                v != null ? v.ProfitFactor.ToString("F2")   : "-",
                v != null ? v.Trades.ToString()             : "-",
                r.ParameterSummary);

            var row = _gridResults.Rows[rowIdx];
            row.Tag = r;

            if (r.Rank <= 3)
                row.DefaultCellStyle.BackColor = Color.FromArgb(210, 255, 210);
            else if (r.OverallScore < 0)
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 225, 225);
        }
    }

    // ── Validation ───────────────────────────────────────────────────────────

    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(_cmbSymbol.Text))
        {
            MessageBox.Show("Symbol is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_cmbTimeframe.SelectedItem == null)
        {
            MessageBox.Show("Timeframe is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_cmbStrategy.SelectedItem == null)
        {
            MessageBox.Show("Strategy type is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_dtpEnd.Value <= _dtpStart.Value)
        {
            MessageBox.Show("End date must be after start date.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetRunning(bool running)
    {
        _btnRunBacktest.Enabled = !running;
        _btnOptimize.Enabled    = !running;
        _btnCancel.Enabled      = running;
        if (!running)
        {
            _progressBar.Value = 0;
            _lblProgress.Text  = "";
        }
    }

    private void StartElapsedTimer()
    {
        _elapsedTimer?.Stop();
        _elapsedTimer?.Dispose();
        _elapsedTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _elapsedTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.UtcNow - _runStart;
            _lblElapsed.Text = $"Elapsed: {elapsed:mm\\:ss}";
        };
        _elapsedTimer.Start();
    }

    private static Label SectionLabel(string text, int x, int y) =>
        new Label
        {
            Text      = text,
            Left      = x,
            Top       = y,
            AutoSize  = true,
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 102, 153),
        };

    private static Label MakeLabel(string text, int x, int y) =>
        new Label { Text = text, Left = x, Top = y + 3, AutoSize = true };

    private static string CsvField(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _elapsedTimer?.Stop();
        _elapsedTimer?.Dispose();
        base.OnFormClosed(e);
    }
}
