using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Application.Optimization;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Optimization;

namespace CryptoResearchTool.UI;

/// <summary>
/// Strategy Parameter Optimizer form.
///
/// Lets the user select a strategy type, define parameter ranges, choose a search mode
/// and objective, optionally enable a train/validation split, run the optimization, view
/// ranked results, export them to CSV, and apply the best parameter set back to the
/// loaded strategy configuration.
/// </summary>
public partial class OptimizerForm : Form
{
    // ── Dependencies ─────────────────────────────────────────────────────────
    private readonly IOptimizationEngine       _engine;
    private readonly List<StrategyConfiguration> _strategies;
    private readonly AppConfiguration           _appConfig;

    // ── UI controls ──────────────────────────────────────────────────────────

    // Strategy / market
    private ComboBox     _cmbStrategy   = null!;
    private ComboBox     _cmbSymbol     = null!;
    private ComboBox     _cmbTimeframe  = null!;
    private DateTimePicker _dtpStart    = null!;
    private DateTimePicker _dtpEnd      = null!;
    private NumericUpDown  _nudCapital  = null!;
    private NumericUpDown  _nudFee      = null!;
    private NumericUpDown  _nudSlippage = null!;

    // Search mode / objective
    private RadioButton  _rbGrid        = null!;
    private RadioButton  _rbRandom      = null!;
    private RadioButton  _rbRetPct      = null!;
    private RadioButton  _rbSharpe      = null!;
    private RadioButton  _rbPF          = null!;
    private RadioButton  _rbRobust      = null!;

    // Train/validation
    private CheckBox      _chkValidation = null!;
    private NumericUpDown _nudTrainPct   = null!;
    private Label         _lblValPct     = null!;

    // Search settings
    private NumericUpDown _nudMaxCombos   = null!;
    private NumericUpDown _nudRandomCount = null!;
    private Label         _lblRandomCount = null!;

    // Parameter range editor
    private DataGridView _gridParams = null!;

    // Progress
    private ProgressBar  _progressBar    = null!;
    private Label        _lblProgress    = null!;
    private Label        _lblElapsed     = null!;
    private Button       _btnRun         = null!;
    private Button       _btnCancel      = null!;

    // Results
    private DataGridView _gridResults   = null!;
    private Label        _lblResultCount = null!;

    // Action buttons
    private Button _btnApply       = null!;
    private Button _btnExportCsv   = null!;
    private Button _btnExportTopN  = null!;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private CancellationTokenSource?    _cts;
    private List<OptimizationResult>    _results = new();
    private System.Windows.Forms.Timer? _elapsedTimer;
    private DateTime                    _runStart;

    // ── Constructor ──────────────────────────────────────────────────────────

    public OptimizerForm(
        IOptimizationEngine engine,
        List<StrategyConfiguration> strategies,
        AppConfiguration appConfig)
    {
        _engine     = engine;
        _strategies = strategies;
        _appConfig  = appConfig;

        InitializeComponent();
        PopulateStrategyCombo();
        UpdateValidationLabel();
        UpdateRandomSearchVisibility();
    }

    // ── Form layout ──────────────────────────────────────────────────────────

    private void InitializeComponent()
    {
        Text            = "Strategy Parameter Optimizer";
        Size            = new Size(1340, 900);
        MinimumSize     = new Size(1100, 750);
        StartPosition   = FormStartPosition.CenterParent;
        BackColor       = Color.FromArgb(240, 242, 245);

        // ── Outer vertical split: config+params (top) | progress+results (bottom)
        var outerSplit = new SplitContainer
        {
            Dock              = DockStyle.Fill,
            Orientation       = Orientation.Horizontal,
            SplitterDistance  = 390,
            Panel1MinSize     = 300,
            Panel2MinSize     = 280,
            BackColor         = Color.FromArgb(240, 242, 245),
        };

        // ── Top-half inner split: settings (left) | param ranges (right)
        var topSplit = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            Orientation      = Orientation.Vertical,
            SplitterDistance = 340,
            Panel1MinSize    = 280,
            Panel2MinSize    = 400,
            BackColor        = Color.FromArgb(240, 242, 245),
        };

        topSplit.Panel1.Controls.Add(BuildSettingsPanel());
        topSplit.Panel2.Controls.Add(BuildParamRangePanel());

        outerSplit.Panel1.Controls.Add(topSplit);
        outerSplit.Panel2.Controls.Add(BuildResultsPanel());

        Controls.Add(outerSplit);
    }

    // ── Settings panel (left column) ─────────────────────────────────────────

    private Panel BuildSettingsPanel()
    {
        var panel = new Panel
        {
            Dock    = DockStyle.Fill,
            Padding = new Padding(8),
        };

        int y = 4;
        int lw = 100; // label width
        int cw = 160; // control width

        // ── Strategy ─────────────────────────────────────────────────────────
        panel.Controls.Add(SectionLabel("Strategy & Market", 0, y)); y += 22;

        panel.Controls.Add(MakeLabel("Strategy:", 0, y));
        _cmbStrategy = new ComboBox { Left = lw, Top = y, Width = cw + 30, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbStrategy.SelectedIndexChanged += OnStrategyChanged;
        panel.Controls.Add(_cmbStrategy);
        y += 26;

        panel.Controls.Add(MakeLabel("Symbol:", 0, y));
        _cmbSymbol = new ComboBox { Left = lw, Top = y, Width = cw, DropDownStyle = ComboBoxStyle.DropDown };
        _cmbSymbol.Items.AddRange(new object[] { "BTCUSDT","ETHUSDT","BNBUSDT","SOLUSDT","XRPUSDT","ADAUSDT","DOGEUSDT" });
        _cmbSymbol.Text = _appConfig.Historical.Symbol;
        panel.Controls.Add(_cmbSymbol);
        y += 26;

        panel.Controls.Add(MakeLabel("Timeframe:", 0, y));
        _cmbTimeframe = new ComboBox { Left = lw, Top = y, Width = cw, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbTimeframe.Items.AddRange(new object[] { "1m","5m","15m","30m","1h","2h","4h","1d" });
        _cmbTimeframe.SelectedItem = _appConfig.Historical.Timeframe;
        if (_cmbTimeframe.SelectedIndex < 0) _cmbTimeframe.SelectedIndex = 4;
        panel.Controls.Add(_cmbTimeframe);
        y += 26;

        panel.Controls.Add(MakeLabel("Start:", 0, y));
        _dtpStart = new DateTimePicker { Left = lw, Top = y, Width = cw, Format = DateTimePickerFormat.Short, Value = _appConfig.Historical.StartDate };
        panel.Controls.Add(_dtpStart);
        y += 26;

        panel.Controls.Add(MakeLabel("End:", 0, y));
        _dtpEnd = new DateTimePicker { Left = lw, Top = y, Width = cw, Format = DateTimePickerFormat.Short, Value = _appConfig.Historical.EndDate };
        panel.Controls.Add(_dtpEnd);
        y += 26;

        panel.Controls.Add(MakeLabel("Capital ($):", 0, y));
        _nudCapital = new NumericUpDown { Left = lw, Top = y, Width = cw, Minimum = 1, Maximum = 10_000_000, DecimalPlaces = 2, Value = (decimal)_appConfig.Historical.InitialCapital, Increment = 100 };
        panel.Controls.Add(_nudCapital);
        y += 26;

        panel.Controls.Add(MakeLabel("Fee %:", 0, y));
        _nudFee = new NumericUpDown { Left = lw, Top = y, Width = 80, Minimum = 0, Maximum = 5, DecimalPlaces = 3, Value = (decimal)_appConfig.Historical.FeePercent, Increment = 0.01m };
        panel.Controls.Add(_nudFee);
        y += 26;

        panel.Controls.Add(MakeLabel("Slippage %:", 0, y));
        _nudSlippage = new NumericUpDown { Left = lw, Top = y, Width = 80, Minimum = 0, Maximum = 5, DecimalPlaces = 3, Value = (decimal)_appConfig.Historical.SlippagePercent, Increment = 0.01m };
        panel.Controls.Add(_nudSlippage);
        y += 32;

        // ── Search mode ───────────────────────────────────────────────────────
        panel.Controls.Add(SectionLabel("Search Mode", 0, y)); y += 22;

        _rbGrid   = new RadioButton { Text = "Grid Search",   Left = 0, Top = y, Width = 120, Checked = true };
        _rbRandom = new RadioButton { Text = "Random Search", Left = 125, Top = y, Width = 120 };
        _rbGrid.CheckedChanged   += (_, _) => UpdateRandomSearchVisibility();
        _rbRandom.CheckedChanged += (_, _) => UpdateRandomSearchVisibility();
        panel.Controls.AddRange(new Control[] { _rbGrid, _rbRandom });
        y += 26;

        panel.Controls.Add(MakeLabel("Max combos:", 0, y));
        _nudMaxCombos = new NumericUpDown { Left = lw, Top = y, Width = 90, Minimum = 1, Maximum = 50_000, Value = 500, Increment = 100 };
        panel.Controls.Add(_nudMaxCombos);
        y += 26;

        _lblRandomCount = MakeLabel("Samples:", 0, y);
        _nudRandomCount = new NumericUpDown { Left = lw, Top = y, Width = 90, Minimum = 10, Maximum = 10_000, Value = 200, Increment = 50 };
        panel.Controls.Add(_lblRandomCount);
        panel.Controls.Add(_nudRandomCount);
        y += 32;

        // ── Objective ─────────────────────────────────────────────────────────
        panel.Controls.Add(SectionLabel("Objective", 0, y)); y += 22;

        _rbRetPct = new RadioButton { Text = "Return %",     Left = 0,   Top = y, Width = 90 };
        _rbSharpe = new RadioButton { Text = "Sharpe",       Left = 95,  Top = y, Width = 70 };
        panel.Controls.AddRange(new Control[] { _rbRetPct, _rbSharpe });
        y += 24;
        _rbPF     = new RadioButton { Text = "Profit Factor", Left = 0,   Top = y, Width = 105 };
        _rbRobust = new RadioButton { Text = "Robust Score ★", Left = 110, Top = y, Width = 130, Checked = true };
        panel.Controls.AddRange(new Control[] { _rbPF, _rbRobust });
        y += 32;

        // ── Train / validation split ──────────────────────────────────────────
        panel.Controls.Add(SectionLabel("Train / Validation Split", 0, y)); y += 22;

        _chkValidation = new CheckBox { Text = "Enable validation split", Left = 0, Top = y, Width = 200, Checked = true };
        _chkValidation.CheckedChanged += (_, _) => UpdateValidationLabel();
        panel.Controls.Add(_chkValidation);
        y += 26;

        panel.Controls.Add(MakeLabel("Train %:", 0, y));
        _nudTrainPct = new NumericUpDown { Left = lw, Top = y, Width = 70, Minimum = 10, Maximum = 95, Value = 70, Increment = 5 };
        _nudTrainPct.ValueChanged += (_, _) => UpdateValidationLabel();
        panel.Controls.Add(_nudTrainPct);

        _lblValPct = new Label { Left = lw + 76, Top = y + 3, AutoSize = true, ForeColor = Color.DimGray };
        panel.Controls.Add(_lblValPct);
        y += 36;

        // ── Run / Cancel ──────────────────────────────────────────────────────
        _btnRun    = new Button { Text = "▶ Run Optimization", Left = 0, Top = y, Width = 160, Height = 34, BackColor = Color.FromArgb(40,167,69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnCancel = new Button { Text = "■ Cancel",           Left = 168, Top = y, Width = 90, Height = 34, BackColor = Color.FromArgb(220,53,69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };
        _btnRun.Click    += OnRunClicked;
        _btnCancel.Click += OnCancelClicked;
        panel.Controls.AddRange(new Control[] { _btnRun, _btnCancel });

        return panel;
    }

    // ── Parameter range panel (right column) ─────────────────────────────────

    private Panel BuildParamRangePanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };

        var titleLabel = SectionLabel("Parameter Ranges (check parameters to include in search)", 0, 0);
        titleLabel.Dock = DockStyle.Top;
        panel.Controls.Add(titleLabel);

        _gridParams = new DataGridView
        {
            Dock            = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible     = false,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor       = Color.White,
            BorderStyle           = BorderStyle.Fixed3D,
        };

        var colEnabled = new DataGridViewCheckBoxColumn { Name = "colEnabled", HeaderText = "✓",  FillWeight = 8,  ReadOnly = false };
        var colName    = new DataGridViewTextBoxColumn  { Name = "colName",    HeaderText = "Parameter", FillWeight = 35, ReadOnly = true };
        var colMin     = new DataGridViewTextBoxColumn  { Name = "colMin",     HeaderText = "Min",    FillWeight = 15 };
        var colMax     = new DataGridViewTextBoxColumn  { Name = "colMax",     HeaderText = "Max",    FillWeight = 15 };
        var colStep    = new DataGridViewTextBoxColumn  { Name = "colStep",    HeaderText = "Step",   FillWeight = 15 };
        var colDesc    = new DataGridViewTextBoxColumn  { Name = "colDesc",    HeaderText = "Description", FillWeight = 50, ReadOnly = true };

        _gridParams.Columns.AddRange(colEnabled, colName, colMin, colMax, colStep, colDesc);
        _gridParams.CellFormatting    += OnParamGridCellFormatting;

        panel.Controls.Add(_gridParams);
        return panel;
    }

    // ── Results panel (bottom half) ──────────────────────────────────────────

    private Panel BuildResultsPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };

        // Progress bar row
        var progressPanel = new Panel { Dock = DockStyle.Top, Height = 30 };
        _progressBar = new ProgressBar { Left = 0, Top = 4, Width = 460, Height = 18, Style = ProgressBarStyle.Continuous };
        _lblProgress = new Label { Left = 470, Top = 7, AutoSize = true };
        _lblElapsed  = new Label { Left = 780, Top = 7, AutoSize = true, ForeColor = Color.DimGray };
        progressPanel.Controls.AddRange(new Control[] { _progressBar, _lblProgress, _lblElapsed });
        panel.Controls.Add(progressPanel);

        // Results header row
        var headerPanel = new Panel { Dock = DockStyle.Top, Height = 26 };
        _lblResultCount = new Label { Left = 0, Top = 5, AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _btnApply      = new Button { Text = "✔ Apply Selected to Strategy", Left = 0, Top = 1, Width = 200, Height = 24, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0,122,204), ForeColor = Color.White, Enabled = false };
        _btnExportCsv  = new Button { Text = "📊 Export All CSV",            Left = 0, Top = 1, Width = 130, Height = 24, FlatStyle = FlatStyle.Flat, Enabled = false };
        _btnExportTopN = new Button { Text = "📊 Export Top 20 CSV",         Left = 0, Top = 1, Width = 130, Height = 24, FlatStyle = FlatStyle.Flat, Enabled = false };
        // anchor right
        headerPanel.SizeChanged += (_, _) =>
        {
            _btnApply.Left     = headerPanel.Width - 210;
            _btnExportCsv.Left = headerPanel.Width - 210 - 140;
            _btnExportTopN.Left = headerPanel.Width - 210 - 140 - 140;
        };
        _btnApply.Click      += OnApplyClicked;
        _btnExportCsv.Click  += (_, _) => ExportCsv(int.MaxValue);
        _btnExportTopN.Click += (_, _) => ExportCsv(20);
        headerPanel.Controls.AddRange(new Control[] { _lblResultCount, _btnApply, _btnExportCsv, _btnExportTopN });
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
        BuildResultsGridColumns();
        _gridResults.SelectionChanged += (_, _) => _btnApply.Enabled = _gridResults.SelectedRows.Count > 0 && _strategies.Count > 0;
        panel.Controls.Add(_gridResults);

        return panel;
    }

    private void BuildResultsGridColumns()
    {
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRank",    HeaderText = "Rank",          FillWeight = 5  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colScore",   HeaderText = "Score",         FillWeight = 8  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTrRet",   HeaderText = "Train Ret%",    FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTrDD",    HeaderText = "Train MaxDD%",  FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTrSharpe",HeaderText = "Train Sharpe",  FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTrPF",    HeaderText = "Train PF",      FillWeight = 7  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTrTrades",HeaderText = "Train Trades",  FillWeight = 7  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTrWR",    HeaderText = "Train WR%",     FillWeight = 7  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValRet",  HeaderText = "Val Ret%",      FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValDD",   HeaderText = "Val MaxDD%",    FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValSharpe",HeaderText ="Val Sharpe",    FillWeight = 9  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValPF",   HeaderText = "Val PF",        FillWeight = 7  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValTrades",HeaderText= "Val Trades",    FillWeight = 7  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValWR",   HeaderText = "Val WR%",       FillWeight = 7  });
        _gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colParams",  HeaderText = "Parameters",    FillWeight = 50 });
    }

    // ── Strategy combo population ─────────────────────────────────────────────

    private void PopulateStrategyCombo()
    {
        _cmbStrategy.Items.Clear();

        // Add loaded strategy configurations by name (if any exist)
        foreach (var s in _strategies)
            _cmbStrategy.Items.Add($"{s.Name} ({s.Type})");

        // Always include raw strategy types from the registry
        foreach (var type in StrategyParameterDescriptorRegistry.KnownStrategyTypes)
        {
            if (!_strategies.Any(s => s.Type == type))
                _cmbStrategy.Items.Add($"[Template] {type}");
        }

        if (_cmbStrategy.Items.Count > 0)
            _cmbStrategy.SelectedIndex = 0;
    }

    private StrategyConfiguration? GetSelectedBaseStrategy()
    {
        if (_cmbStrategy.SelectedIndex < 0) return null;
        string text = _cmbStrategy.SelectedItem?.ToString() ?? "";

        // Loaded strategy
        var loaded = _strategies.FirstOrDefault(s => text.StartsWith(s.Name));
        if (loaded != null) return loaded;

        // Template: create a minimal config for the strategy type
        var typeName = text.Replace("[Template] ", "").Trim();
        return new StrategyConfiguration { Type = typeName, Name = typeName, Enabled = true };
    }

    // ── Parameter range grid population ──────────────────────────────────────

    private void OnStrategyChanged(object? sender, EventArgs e)
    {
        _gridParams.Rows.Clear();

        var baseStrategy = GetSelectedBaseStrategy();
        if (baseStrategy == null) return;

        var descriptors = StrategyParameterDescriptorRegistry.GetDescriptors(baseStrategy.Type);
        foreach (var d in descriptors)
        {
            bool isInt = d.Type == ParameterType.Integer;
            int rowIdx = _gridParams.Rows.Add(
                false,                          // Enabled
                d.ParameterName,
                d.DefaultMin.ToString(isInt ? "F0" : "F4"),
                d.DefaultMax.ToString(isInt ? "F0" : "F4"),
                d.DefaultStep.ToString(isInt ? "F0" : "F4"),
                d.Description);

            // Tag row with the descriptor for later retrieval
            _gridParams.Rows[rowIdx].Tag = d;
        }
    }

    // ── Cell formatting: dim disabled rows ───────────────────────────────────

    private void OnParamGridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridParams.Rows.Count) return;
        var row  = _gridParams.Rows[e.RowIndex];
        bool enabled = row.Cells["colEnabled"].Value is true;
        if (e.CellStyle != null)
            e.CellStyle.ForeColor = enabled ? Color.Black : Color.Silver;
    }

    // ── Run / Cancel ──────────────────────────────────────────────────────────

    private async void OnRunClicked(object? sender, EventArgs e)
    {
        var request = BuildRequest();
        if (request == null) return;

        // Estimate combinations and warn if excessively large
        long estimate = _engine.EstimateCombinationCount(request);
        if (estimate > request.MaxCombinations * 5 && request.SearchMode == OptimizationSearchMode.GridSearch)
        {
            var ans = MessageBox.Show(
                $"Grid search estimates ~{estimate:N0} combinations which is much larger than " +
                $"the limit of {request.MaxCombinations:N0}.\n\n" +
                "The search will be truncated. Consider using Random Search or reducing the parameter ranges.\n\n" +
                "Continue?",
                "Many Combinations", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (ans != DialogResult.Yes) return;
        }

        SetRunning(true);
        _results.Clear();
        _gridResults.Rows.Clear();
        _lblResultCount.Text = "Running…";

        _cts    = new CancellationTokenSource();
        _runStart = DateTime.UtcNow;

        StartElapsedTimer();

        var progressReporter = new Progress<OptimizationProgress>(p =>
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke(() =>
            {
                int pct = p.Total > 0 ? (int)(p.CurrentIndex * 100L / p.Total) : 0;
                _progressBar.Value = Math.Max(0, Math.Min(100, pct));
                _lblProgress.Text  = p.Status;
            });
        });

        try
        {
            _results = await Task.Run(
                () => _engine.RunAsync(request, progressReporter, _cts.Token),
                _cts.Token);

            PopulateResultsGrid(_results);
            _lblResultCount.Text = $"{_results.Count} results  (best score: {_results.FirstOrDefault()?.OverallScore:F2})";
            _btnExportCsv.Enabled  = _results.Count > 0;
            _btnExportTopN.Enabled = _results.Count > 0;
        }
        catch (OperationCanceledException)
        {
            _lblResultCount.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Optimization failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblResultCount.Text = "Error.";
        }
        finally
        {
            SetRunning(false);
            _elapsedTimer?.Stop();
        }
    }

    private void OnCancelClicked(object? sender, EventArgs e) => _cts?.Cancel();

    private OptimizationRequest? BuildRequest()
    {
        var baseStrategy = GetSelectedBaseStrategy();
        if (baseStrategy == null)
        {
            MessageBox.Show("Please select a strategy.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        var ranges = CollectEnabledRanges();
        if (ranges.Count == 0)
        {
            MessageBox.Show("Enable at least one parameter range before running.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (_dtpEnd.Value <= _dtpStart.Value)
        {
            MessageBox.Show("End date must be after start date.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        return new OptimizationRequest
        {
            BaseStrategy         = baseStrategy,
            Symbol               = _cmbSymbol.Text.Trim().ToUpperInvariant(),
            Timeframe            = _cmbTimeframe.SelectedItem?.ToString() ?? "1h",
            StartDate            = _dtpStart.Value.Date,
            EndDate              = _dtpEnd.Value.Date.AddDays(1).AddSeconds(-1),
            InitialCapital       = _nudCapital.Value,
            FeePercent           = _nudFee.Value,
            SlippagePercent      = _nudSlippage.Value,
            SearchMode           = _rbGrid.Checked ? OptimizationSearchMode.GridSearch : OptimizationSearchMode.RandomSearch,
            Objective            = GetSelectedObjective(),
            ParameterRanges      = ranges,
            MaxCombinations      = (int)_nudMaxCombos.Value,
            RandomSampleCount    = (int)_nudRandomCount.Value,
            EnableValidationSplit = _chkValidation.Checked,
            TrainPercent         = _nudTrainPct.Value,
            UseLocalCache        = true,
        };
    }

    private List<StrategyParameterRange> CollectEnabledRanges()
    {
        var list = new List<StrategyParameterRange>();
        foreach (DataGridViewRow row in _gridParams.Rows)
        {
            bool enabled = row.Cells["colEnabled"].Value is true;
            if (!enabled) continue;

            var descriptor = row.Tag as StrategyParameterDescriptor;
            bool isInt = descriptor?.Type == ParameterType.Integer;

            if (!decimal.TryParse(row.Cells["colMin"].Value?.ToString(),  out var min))  continue;
            if (!decimal.TryParse(row.Cells["colMax"].Value?.ToString(),  out var max))  continue;
            if (!decimal.TryParse(row.Cells["colStep"].Value?.ToString(), out var step) || step <= 0) continue;

            list.Add(new StrategyParameterRange
            {
                ParameterName = row.Cells["colName"].Value?.ToString() ?? "",
                MinValue      = min,
                MaxValue      = max,
                Step          = step,
                IsEnabled     = true,
                IsInteger     = isInt,
            });
        }
        return list;
    }

    private OptimizationObjective GetSelectedObjective()
    {
        if (_rbRetPct.Checked) return OptimizationObjective.ReturnPercent;
        if (_rbSharpe.Checked) return OptimizationObjective.Sharpe;
        if (_rbPF.Checked)     return OptimizationObjective.ProfitFactor;
        return OptimizationObjective.RobustScore;
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
                t.WinRate.ToString("F1"),
                v != null ? v.ReturnPct.ToString("F2")      : "-",
                v != null ? v.MaxDrawdownPct.ToString("F2") : "-",
                v != null ? v.SharpeRatio.ToString("F3")    : "-",
                v != null ? v.ProfitFactor.ToString("F2")   : "-",
                v != null ? v.Trades.ToString()             : "-",
                v != null ? v.WinRate.ToString("F1")        : "-",
                r.ParameterSummary);

            var row = _gridResults.Rows[rowIdx];
            row.Tag = r;

            // Colour-code by score
            if (r.Rank <= 3)
                row.DefaultCellStyle.BackColor = Color.FromArgb(210, 255, 210);
            else if (r.OverallScore < 0)
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 225, 225);
        }
    }

    // ── Apply best result to strategy ─────────────────────────────────────────

    private void OnApplyClicked(object? sender, EventArgs e)
    {
        if (_gridResults.SelectedRows.Count == 0) return;
        var result = _gridResults.SelectedRows[0].Tag as OptimizationResult;
        if (result == null) return;

        // Find the target strategy (match by type)
        var baseStrategy = GetSelectedBaseStrategy();
        var target = _strategies.FirstOrDefault(s => s.Type == baseStrategy?.Type) ?? baseStrategy;
        if (target == null) return;

        var ans = MessageBox.Show(
            $"Apply these parameters to strategy \"{target.Name}\" ({target.Type})?\n\n" +
            $"{result.ParameterSummary}\n\n" +
            "This updates the in-memory configuration. " +
            "Save strategies.json manually to persist the changes.",
            "Apply Parameters",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (ans != DialogResult.Yes) return;

        StrategyConfigurationCloner.ApplyParameters(target, result.ParameterValues);

        MessageBox.Show(
            $"Parameters applied to \"{target.Name}\".\n\n" +
            "Note: these changes are in-memory only. Close this dialog and save strategies.json to persist them.",
            "Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── CSV export ────────────────────────────────────────────────────────────

    private void ExportCsv(int topN)
    {
        if (_results.Count == 0) return;

        using var dlg = new SaveFileDialog
        {
            Filter   = "CSV|*.csv",
            FileName = $"optimizer_{_cmbSymbol.Text}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var rows = new List<string>();

        // Header
        rows.Add("Rank,Strategy,Symbol,Timeframe,OverallScore," +
                 "Train_RetPct,Train_MaxDDPct,Train_Sharpe,Train_ProfitFactor,Train_Trades,Train_WinRate,Train_Score," +
                 "Val_RetPct,Val_MaxDDPct,Val_Sharpe,Val_ProfitFactor,Val_Trades,Val_WinRate,Val_Score," +
                 "Parameters");

        foreach (var r in _results.Take(topN))
        {
            var t = r.TrainMetrics;
            var v = r.ValidationMetrics;
            rows.Add(string.Join(",",
                r.Rank,
                CsvField(r.StrategyName),
                CsvField(r.Symbol),
                CsvField(r.Timeframe),
                r.OverallScore.ToString("F4"),
                t.ReturnPct.ToString("F4"),
                t.MaxDrawdownPct.ToString("F4"),
                t.SharpeRatio.ToString("F4"),
                t.ProfitFactor.ToString("F4"),
                t.Trades,
                t.WinRate.ToString("F2"),
                t.Score.ToString("F4"),
                v != null ? v.ReturnPct.ToString("F4")      : "",
                v != null ? v.MaxDrawdownPct.ToString("F4") : "",
                v != null ? v.SharpeRatio.ToString("F4")    : "",
                v != null ? v.ProfitFactor.ToString("F4")   : "",
                v != null ? v.Trades.ToString()             : "",
                v != null ? v.WinRate.ToString("F2")        : "",
                v != null ? v.Score.ToString("F4")          : "",
                CsvField(r.ParameterSummary)));
        }

        File.WriteAllLines(dlg.FileName, rows);
        MessageBox.Show($"Exported {rows.Count - 1} rows to:\n{dlg.FileName}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string CsvField(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetRunning(bool running)
    {
        _btnRun.Enabled    = !running;
        _btnCancel.Enabled = running;
        if (!running)
        {
            _progressBar.Value = 0;
        }
    }

    private void UpdateValidationLabel()
    {
        bool en = _chkValidation.Checked;
        _nudTrainPct.Enabled = en;
        int valPct = en ? (int)(100 - _nudTrainPct.Value) : 0;
        _lblValPct.Text = en ? $"Val: {valPct}%" : "";
    }

    private void UpdateRandomSearchVisibility()
    {
        bool rand = _rbRandom.Checked;
        _lblRandomCount.Visible = rand;
        _nudRandomCount.Visible = rand;
        _nudMaxCombos.Enabled   = !rand;
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

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _elapsedTimer?.Stop();
        _elapsedTimer?.Dispose();
        base.OnFormClosed(e);
    }
}
