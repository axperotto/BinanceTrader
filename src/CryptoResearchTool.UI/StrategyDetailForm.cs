using CryptoResearchTool.Application.Services;

namespace CryptoResearchTool.UI;

public class StrategyDetailForm : Form
{
    private readonly StrategyRunner _runner;

    public StrategyDetailForm(StrategyRunner runner)
    {
        _runner = runner;
        InitializeComponent();
        PopulateData();
    }

    private void InitializeComponent()
    {
        Text = $"Strategy Detail - {_runner.Strategy.Name}";
        Size = new Size(700, 500);
        StartPosition = FormStartPosition.CenterParent;

        var tabs = new TabControl { Dock = DockStyle.Fill };

        var tabMetrics = new TabPage("Metrics");
        var metricsGrid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false, RowHeadersVisible = false,
            Name = "metricsGrid"
        };
        metricsGrid.Columns.Add("Metric", "Metric");
        metricsGrid.Columns.Add("Value", "Value");
        tabMetrics.Controls.Add(metricsGrid);

        var tabTrades = new TabPage("Trades");
        var tradesGrid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false, RowHeadersVisible = false,
            Name = "tradesGrid"
        };
        tradesGrid.Columns.AddRange(
            new DataGridViewTextBoxColumn { Name = "colEntry", HeaderText = "Entry", FillWeight = 80 },
            new DataGridViewTextBoxColumn { Name = "colExit", HeaderText = "Exit", FillWeight = 80 },
            new DataGridViewTextBoxColumn { Name = "colQty", HeaderText = "Quantity", FillWeight = 80 },
            new DataGridViewTextBoxColumn { Name = "colPnL", HeaderText = "PnL ($)", FillWeight = 70 },
            new DataGridViewTextBoxColumn { Name = "colPnLPct", HeaderText = "PnL %", FillWeight = 60 },
            new DataGridViewTextBoxColumn { Name = "colReason", HeaderText = "Reason", FillWeight = 150 }
        );
        tabTrades.Controls.Add(tradesGrid);

        tabs.TabPages.Add(tabMetrics);
        tabs.TabPages.Add(tabTrades);

        var btnRefresh = new Button { Text = "Refresh", Dock = DockStyle.Bottom, Height = 30 };
        btnRefresh.Click += (s, e) => PopulateData();

        Controls.Add(tabs);
        Controls.Add(btnRefresh);
    }

    private void PopulateData()
    {
        var m = _runner.CurrentMetrics;
        var metricsGrid = (DataGridView)Controls.OfType<TabControl>().First().TabPages[0].Controls[0];
        metricsGrid.Rows.Clear();
        metricsGrid.Rows.Add("Strategy", m.StrategyName);
        metricsGrid.Rows.Add("Initial Capital", $"${m.InitialCapital:F2}");
        metricsGrid.Rows.Add("Current Equity", $"${m.CurrentEquity:F2}");
        metricsGrid.Rows.Add("Net Profit", $"${m.NetProfit:F2}");
        metricsGrid.Rows.Add("Return %", $"{m.ReturnPercent:F2}%");
        metricsGrid.Rows.Add("Realized PnL", $"${m.RealizedPnL:F2}");
        metricsGrid.Rows.Add("Unrealized PnL", $"${m.UnrealizedPnL:F2}");
        metricsGrid.Rows.Add("Total Trades", m.TotalTrades.ToString());
        metricsGrid.Rows.Add("Winning Trades", m.WinningTrades.ToString());
        metricsGrid.Rows.Add("Losing Trades", m.LosingTrades.ToString());
        metricsGrid.Rows.Add("Win Rate", $"{m.WinRate:F1}%");
        metricsGrid.Rows.Add("Profit Factor", $"{m.ProfitFactor:F2}");
        metricsGrid.Rows.Add("Avg Trade PnL", $"${m.AverageTradePnL:F2}");
        metricsGrid.Rows.Add("Expectancy", $"${m.Expectancy:F2}");
        metricsGrid.Rows.Add("Max Drawdown", $"${m.MaxDrawdown:F2} ({m.MaxDrawdownPercent:F2}%)");
        metricsGrid.Rows.Add("Sharpe Ratio", $"{m.SharpeRatio:F3}");
        metricsGrid.Rows.Add("Signals Generated", m.SignalsGenerated.ToString());
        metricsGrid.Rows.Add("Signals Executed", m.SignalsExecuted.ToString());
        metricsGrid.Rows.Add("Last Updated", m.LastUpdated.ToString("HH:mm:ss"));

        var tradesGrid = (DataGridView)Controls.OfType<TabControl>().First().TabPages[1].Controls[0];
        tradesGrid.Rows.Clear();
        foreach (var t in _runner.Portfolio.CompletedTrades)
        {
            tradesGrid.Rows.Add(
                $"{t.EntryPrice:F2}",
                $"{t.ExitPrice:F2}",
                $"{t.Quantity:F6}",
                $"{t.PnL:F2}",
                $"{t.PnLPercent:F2}%",
                t.ExitReason
            );
            tradesGrid.Rows[tradesGrid.Rows.Count - 1].DefaultCellStyle.BackColor = t.IsWinner ? Color.FromArgb(220, 255, 220) : Color.FromArgb(255, 220, 220);
        }
    }
}
