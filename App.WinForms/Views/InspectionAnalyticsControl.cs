using App.Core.Models;
using App.WinForms.Controllers;
using App.WinForms.ViewModels;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace App.WinForms.Views;

internal sealed class InspectionAnalyticsControl : UserControl
{
    private static readonly Color PageBackground = Color.FromArgb(10, 10, 15);
    private static readonly Color SurfaceBackground = Color.FromArgb(28, 30, 40);
    private static readonly Color SurfaceBorder = Color.FromArgb(80, 85, 110);
    private static readonly Color HeaderBackground = Color.FromArgb(22, 24, 33);
    private static readonly Color TextPrimaryColor = Color.FromArgb(255, 255, 255);
    private static readonly Color TextSecondaryColor = Color.FromArgb(210, 215, 230);
    private static readonly Color TextMutedColor = Color.FromArgb(160, 170, 190);
    private static readonly Color AccentBlue = Color.FromArgb(88, 130, 255);
    private static readonly Color SuccessColor = Color.FromArgb(39, 174, 96);
    private static readonly Color WarningColor = Color.FromArgb(241, 196, 15);
    private static readonly Color DangerColor = Color.FromArgb(231, 76, 60);
    private static readonly Color PendingColor = Color.FromArgb(148, 90, 255);

    private readonly InspectionController _inspectionController;
    private readonly Label _generatedAtLabel;
    private readonly Label _totalValueLabel;
    private readonly Label _totalNoteLabel;
    private readonly Label _passRateValueLabel;
    private readonly Label _passRateNoteLabel;
    private readonly Label _pendingValueLabel;
    private readonly Label _pendingNoteLabel;
    private readonly Label _abnormalValueLabel;
    private readonly Label _abnormalNoteLabel;
    private readonly Label _trendSubtitleLabel;
    private readonly Label _statusSubtitleLabel;
    private readonly Label _lineSubtitleLabel;
    private readonly Label _issueSubtitleLabel;
    private readonly Button _refreshButton;
    private readonly BufferedPanel _trendChartPanel;
    private readonly BufferedPanel _statusChartPanel;
    private readonly DataGridView _lineSummaryGrid;
    private readonly DataGridView _issueGrid;

    private InspectionDashboardViewModel _currentDashboard = new();
    private IReadOnlyList<LineSummaryRow> _lineRows = Array.Empty<LineSummaryRow>();
    private IReadOnlyList<AttentionRow> _attentionRows = Array.Empty<AttentionRow>();

    public InspectionAnalyticsControl(InspectionController inspectionController)
    {
        _inspectionController = inspectionController;
        Dock = DockStyle.Fill;
        BackColor = PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);
        Padding = new Padding(30, 20, 30, 20);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        _refreshButton = CreateRefreshButton();
        _generatedAtLabel = CreateInfoLabel();
        var header = BuildHeader();

        var summaryArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12)
        };
        summaryArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        for (var index = 0; index < 4; index++)
        {
            summaryArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        summaryArea.Controls.Add(CreateMetricCard("总记录", AccentBlue, out _totalValueLabel, out _totalNoteLabel), 0, 0);
        summaryArea.Controls.Add(CreateMetricCard("合格率", SuccessColor, out _passRateValueLabel, out _passRateNoteLabel), 1, 0);
        summaryArea.Controls.Add(CreateMetricCard("待闭环", PendingColor, out _pendingValueLabel, out _pendingNoteLabel), 2, 0);
        summaryArea.Controls.Add(CreateMetricCard("异常数", DangerColor, out _abnormalValueLabel, out _abnormalNoteLabel), 3, 0);

        _trendChartPanel = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            Margin = new Padding(14, 0, 14, 14)
        };
        _trendChartPanel.Paint += DrawTrendChart;

        _statusChartPanel = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            Margin = new Padding(14, 0, 14, 14)
        };
        _statusChartPanel.Paint += DrawStatusChart;

        var chartArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12)
        };
        chartArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
        chartArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        chartArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        chartArea.Controls.Add(CreateSectionShell("趋势变化", "最近 8 个时间桶状态变化", out _trendSubtitleLabel, _trendChartPanel), 0, 0);
        chartArea.Controls.Add(CreateSectionShell("状态占比", "按当前筛选结果汇总", out _statusSubtitleLabel, _statusChartPanel), 1, 0);

        _lineSummaryGrid = CreateLineSummaryGrid();
        _issueGrid = CreateIssueGrid();
        var tableArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1
        };
        tableArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
        tableArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        tableArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableArea.Controls.Add(CreateSectionShell("产线汇总", "按产线统计巡检结果", out _lineSubtitleLabel, _lineSummaryGrid), 0, 0);
        tableArea.Controls.Add(CreateSectionShell("最近关注项", "最近预警和异常记录", out _issueSubtitleLabel, _issueGrid), 1, 0);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(summaryArea, 0, 1);
        root.Controls.Add(chartArea, 0, 2);
        root.Controls.Add(tableArea, 0, 3);
        Controls.Add(root);

        ApplyTheme();
        RefreshData();
    }

    public void ApplyTheme()
    {
        BackColor = PageBackground;
        _trendChartPanel.BackColor = SurfaceBackground;
        _statusChartPanel.BackColor = SurfaceBackground;
        ApplyGridTheme(_lineSummaryGrid);
        ApplyGridTheme(_issueGrid);
        _refreshButton.BackColor = HeaderBackground;
        _refreshButton.ForeColor = TextPrimaryColor;
        Invalidate(true);
    }

    public void RefreshData()
    {
        _currentDashboard = _inspectionController.Load(new InspectionFilterViewModel());
        _lineRows = BuildLineRows(_currentDashboard.Records);
        _attentionRows = BuildAttentionRows(_currentDashboard.Records);

        var pendingRows = _currentDashboard.Records
            .Where(record => record.Status != InspectionStatus.Normal && !record.IsClosed)
            .ToList();
        var pendingWarningCount = pendingRows.Count(record => record.Status == InspectionStatus.Warning);
        var pendingAbnormalCount = pendingRows.Count(record => record.Status == InspectionStatus.Abnormal);
        var affectedDeviceCount = _currentDashboard.Records
            .Where(record => record.Status != InspectionStatus.Normal)
            .Select(record => $"{record.LineName}|{record.DeviceName}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var highRiskLine = _lineRows.FirstOrDefault(row => row.AbnormalCount > 0);

        _totalValueLabel.Text = _currentDashboard.TotalCount.ToString();
        _totalNoteLabel.Text = $"正常 {_currentDashboard.NormalCount} / 预警 {_currentDashboard.WarningCount} / 异常 {_currentDashboard.AbnormalCount}";

        _passRateValueLabel.Text = _currentDashboard.PassRateText;
        _passRateNoteLabel.Text = _lineRows.Count == 0
            ? "当前没有可统计的产线"
            : $"覆盖 {_lineRows.Count} 条产线，涉及 {affectedDeviceCount} 台问题设备";

        _pendingValueLabel.Text = pendingRows.Count.ToString();
        _pendingNoteLabel.Text = pendingRows.Count == 0
            ? "当前没有待闭环问题"
            : $"预警 {pendingWarningCount} 条，异常 {pendingAbnormalCount} 条";

        _abnormalValueLabel.Text = _currentDashboard.AbnormalCount.ToString();
        _abnormalNoteLabel.Text = _currentDashboard.AbnormalCount == 0
            ? "暂无异常记录"
            : $"高风险产线：{highRiskLine?.LineName ?? "未分类"}";

        _generatedAtLabel.Text = $"更新时间：{_currentDashboard.GeneratedAt:yyyy-MM-dd HH:mm}";
        _trendSubtitleLabel.Text = BuildTrendSubtitle(_currentDashboard.TrendPoints);
        _statusSubtitleLabel.Text = $"正常 {_currentDashboard.NormalCount} / 预警 {_currentDashboard.WarningCount} / 异常 {_currentDashboard.AbnormalCount}";
        _lineSubtitleLabel.Text = _lineRows.Count == 0
            ? "当前没有产线统计结果"
            : $"按风险优先级排序，共 {_lineRows.Count} 条产线";
        _issueSubtitleLabel.Text = _attentionRows.Count == 0
            ? "当前没有预警和异常记录"
            : $"最近 {_attentionRows.Count} 条需关注记录";

        _lineSummaryGrid.DataSource = _lineRows.ToList();
        _issueGrid.DataSource = _attentionRows.ToList();

        _trendChartPanel.Invalidate();
        _statusChartPanel.Invalidate();
    }

    private Control BuildHeader()
    {
        var shell = CreateSurfacePanel(new Padding(18, 10, 18, 10));
        shell.Margin = new Padding(0, 0, 0, 12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var titlePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        titlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
            ForeColor = TextPrimaryColor,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 2),
            Text = "统计分析"
        };
        var subtitleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextSecondaryColor,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 2),
            Text = "基于巡检记录自动汇总关键指标、趋势和关注项"
        };
        _generatedAtLabel.Dock = DockStyle.Top;
        _generatedAtLabel.Margin = Padding.Empty;

        titlePanel.Controls.Add(titleLabel, 0, 0);
        titlePanel.Controls.Add(subtitleLabel, 0, 1);
        titlePanel.Controls.Add(_generatedAtLabel, 0, 2);

        var actionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0)
        };
        actionPanel.Controls.Add(_refreshButton);

        layout.Controls.Add(titlePanel, 0, 0);
        layout.Controls.Add(actionPanel, 1, 0);
        shell.Controls.Add(layout);
        return shell;
    }

    private static BufferedPanel CreateMetricCard(string title, Color accentColor, out Label valueLabel, out Label noteLabel)
    {
        var card = CreateSurfacePanel(new Padding(16, 14, 16, 14));
        card.Margin = new Padding(0, 0, 12, 0);
        card.Paint += (_, e) =>
        {
            using var accentPen = new Pen(accentColor, 2F);
            e.Graphics.DrawLine(accentPen, 16, 10, 54, 10);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(0, 6, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextMutedColor,
            Margin = new Padding(0, 2, 0, 6),
            Text = title
        };
        valueLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = TextPrimaryColor,
            Margin = new Padding(16, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleRight,
            Text = "0"
        };
        noteLabel = new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 8.5F),
            ForeColor = TextSecondaryColor,
            Margin = new Padding(0, 2, 0, 0),
            TextAlign = ContentAlignment.TopLeft,
            Text = string.Empty
        };

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(valueLabel, 1, 0);
        layout.Controls.Add(noteLabel, 0, 1);
        layout.SetColumnSpan(noteLabel, 2);
        card.Controls.Add(layout);
        return card;
    }

    private static BufferedPanel CreateSectionShell(string title, string subtitle, out Label subtitleLabel, Control body)
    {
        var shell = CreateSurfacePanel(new Padding(0));
        shell.Margin = new Padding(0, 0, 12, 12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(18, 12, 18, 0)
        };

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = TextPrimaryColor,
            Location = new Point(0, 0),
            Text = title
        };
        subtitleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 8.75F),
            ForeColor = TextMutedColor,
            Location = new Point(0, 22),
            Text = subtitle
        };

        header.Controls.Add(titleLabel);
        header.Controls.Add(subtitleLabel);
        body.Dock = DockStyle.Fill;
        body.Margin = new Padding(14, 0, 14, 14);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(body, 0, 1);
        shell.Controls.Add(layout);
        return shell;
    }

    private static BufferedPanel CreateSurfacePanel(Padding padding)
    {
        var panel = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            Padding = padding
        };
        panel.Paint += (_, e) =>
        {
            using var borderPen = new Pen(SurfaceBorder, 1F);
            var rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            e.Graphics.DrawRectangle(borderPen, rect);
        };
        return panel;
    }

    private static Label CreateInfoLabel()
    {
        return new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextMutedColor,
            Text = "更新时间：--"
        };
    }

    private Button CreateRefreshButton()
    {
        var button = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = HeaderBackground,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            ForeColor = TextPrimaryColor,
            Padding = new Padding(14, 6, 14, 6),
            Text = "刷新数据",
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = AccentBlue;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(42, 46, 62);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(34, 38, 50);
        button.Click += (_, _) => RefreshData();
        return button;
    }

    private static DataGridView CreateLineSummaryGrid()
    {
        var grid = CreateGrid();
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LineSummaryRow.LineName), HeaderText = "产线", Width = 120 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LineSummaryRow.TotalCount), HeaderText = "总数", Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LineSummaryRow.NormalCount), HeaderText = "正常", Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LineSummaryRow.WarningCount), HeaderText = "预警", Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LineSummaryRow.AbnormalCount), HeaderText = "异常", Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LineSummaryRow.PassRateText), HeaderText = "合格率", Width = 90 });
        return grid;
    }

    private static DataGridView CreateIssueGrid()
    {
        var grid = CreateGrid();
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AttentionRow.CheckedAt), HeaderText = "时间", Width = 130 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AttentionRow.TargetName), HeaderText = "设备", Width = 210 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AttentionRow.StatusText), HeaderText = "状态", Width = 80 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AttentionRow.Remark), HeaderText = "说明", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        AttachStatusColoring(grid, nameof(AttentionRow.StatusText));
        return grid;
    }

    private static DataGridView CreateGrid()
    {
        var grid = new BufferedDataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            BackgroundColor = SurfaceBackground,
            BorderStyle = BorderStyle.None,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            Dock = DockStyle.Fill,
            EnableHeadersVisualStyles = false,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        grid.RowTemplate.Height = 32;
        ApplyGridTheme(grid);
        return grid;
    }

    private static void ApplyGridTheme(DataGridView grid)
    {
        grid.BackgroundColor = SurfaceBackground;
        grid.GridColor = SurfaceBorder;
        grid.DefaultCellStyle.BackColor = SurfaceBackground;
        grid.DefaultCellStyle.ForeColor = TextSecondaryColor;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 56, 78);
        grid.DefaultCellStyle.SelectionForeColor = TextPrimaryColor;
        grid.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F);
        grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderBackground;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimaryColor;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        grid.ColumnHeadersHeight = 34;
    }

    private static void AttachStatusColoring(DataGridView grid, string statusPropertyName)
    {
        grid.CellFormatting += (_, args) =>
        {
            if (args.Value is not string text)
            {
                return;
            }

            if (grid.Columns[args.ColumnIndex].DataPropertyName != statusPropertyName)
            {
                return;
            }

            var cellStyle = args.CellStyle;
            if (cellStyle is null)
            {
                return;
            }

            cellStyle.ForeColor = text switch
            {
                "正常" => SuccessColor,
                "预警" => WarningColor,
                "异常" => DangerColor,
                _ => TextPrimaryColor
            };
        };
    }

    private void DrawTrendChart(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(SurfaceBackground);

        var points = _currentDashboard.TrendPoints;
        var bounds = new Rectangle(18, 12, Math.Max(0, _trendChartPanel.Width - 36), Math.Max(0, _trendChartPanel.Height - 24));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var labelFont = new Font("Microsoft YaHei UI", 8.5F);
        using var labelBrush = new SolidBrush(TextMutedColor);
        if (points.Count == 0)
        {
            DrawCenteredText(g, bounds, "暂无趋势数据", labelFont, labelBrush);
            return;
        }

        var plotRect = new Rectangle(bounds.X + 34, bounds.Y + 20, bounds.Width - 54, bounds.Height - 62);
        if (plotRect.Width <= 20 || plotRect.Height <= 20)
        {
            return;
        }

        var maxValue = Math.Max(1, points.Max(point => Math.Max(point.NormalCount, Math.Max(point.WarningCount, point.AbnormalCount))));
        using var axisPen = new Pen(SurfaceBorder, 1F);
        using var gridPen = new Pen(Color.FromArgb(55, 62, 80), 1F);

        for (var index = 0; index <= 4; index++)
        {
            var y = plotRect.Bottom - plotRect.Height * index / 4F;
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            var label = Math.Round(maxValue * index / 4F).ToString("0");
            g.DrawString(label, labelFont, labelBrush, bounds.Left, y - 8);
        }

        g.DrawLine(axisPen, plotRect.Left, plotRect.Top, plotRect.Left, plotRect.Bottom);
        g.DrawLine(axisPen, plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom);

        DrawTrendSeries(g, plotRect, points, maxValue, point => point.NormalCount, SuccessColor);
        DrawTrendSeries(g, plotRect, points, maxValue, point => point.WarningCount, WarningColor);
        DrawTrendSeries(g, plotRect, points, maxValue, point => point.AbnormalCount, DangerColor);

        for (var index = 0; index < points.Count; index++)
        {
            var x = plotRect.Left + plotRect.Width * index / Math.Max(1F, points.Count - 1F);
            var label = points[index].Label;
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, labelBrush, x - size.Width / 2F, plotRect.Bottom + 10);
        }

        DrawLegend(g, new Point(plotRect.Right - 158, bounds.Top));
    }

    private void DrawStatusChart(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(SurfaceBackground);

        var counts = new[]
        {
            ("正常", _currentDashboard.NormalCount, SuccessColor),
            ("预警", _currentDashboard.WarningCount, WarningColor),
            ("异常", _currentDashboard.AbnormalCount, DangerColor)
        };

        using var labelFont = new Font("Microsoft YaHei UI", 8.5F);
        using var labelBrush = new SolidBrush(TextMutedColor);

        var total = counts.Sum(item => item.Item2);
        if (total == 0)
        {
            DrawCenteredText(g, new Rectangle(0, 0, _statusChartPanel.Width, _statusChartPanel.Height), "暂无状态统计", labelFont, labelBrush);
            return;
        }

        var contentRect = new Rectangle(14, 10, Math.Max(0, _statusChartPanel.Width - 28), Math.Max(0, _statusChartPanel.Height - 20));
        if (contentRect.Width <= 0 || contentRect.Height <= 0)
        {
            return;
        }

        var legendWidth = Math.Min(168, Math.Max(140, contentRect.Width / 3));
        var compactLegend = contentRect.Width < 360;
        var legendHeight = compactLegend ? counts.Length * 26 : 52;
        var diameter = Math.Min(contentRect.Height - legendHeight - 16, contentRect.Width - 24);
        diameter = Math.Max(100, diameter);
        var donutRect = new Rectangle(
            contentRect.Left + (contentRect.Width - diameter) / 2,
            contentRect.Top,
            diameter,
            diameter);

        var ringWidth = diameter >= 150 ? 22F : 18F;
        using var backPen = new Pen(Color.FromArgb(55, 62, 80), ringWidth);
        g.DrawArc(backPen, donutRect, 0, 360);

        var startAngle = -90F;
        foreach (var (_, value, color) in counts)
        {
            if (value == 0)
            {
                continue;
            }

            var sweepAngle = 360F * value / total;
            using var pen = new Pen(color, ringWidth);
            g.DrawArc(pen, donutRect, startAngle, sweepAngle);
            startAngle += sweepAngle;
        }

        using var valueFont = new Font("Segoe UI", 18F, FontStyle.Bold);
        using var totalBrush = new SolidBrush(TextPrimaryColor);
        var totalText = total.ToString();
        var totalSize = g.MeasureString(totalText, valueFont);
        g.DrawString(totalText, valueFont, totalBrush,
            donutRect.Left + donutRect.Width / 2F - totalSize.Width / 2F,
            donutRect.Top + donutRect.Height / 2F - 24F);
        g.DrawString("总记录", labelFont, labelBrush,
            donutRect.Left + donutRect.Width / 2F - 20F,
            donutRect.Top + donutRect.Height / 2F + 6F);

        var legendArea = new Rectangle(
            contentRect.Left,
            donutRect.Bottom + 10,
            contentRect.Width,
            Math.Max(0, contentRect.Bottom - donutRect.Bottom - 10));
        using var legendTitleFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        using var legendValueFont = new Font("Microsoft YaHei UI", 9F);
        if (compactLegend)
        {
            var legendRowY = legendArea.Top;
            foreach (var (name, value, color) in counts)
            {
                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, legendArea.Left, legendRowY + 8, 10, 10);
                g.DrawString(name, legendTitleFont, totalBrush, legendArea.Left + 16, legendRowY);
                g.DrawString($"{value} / {value * 100F / total:0.0}%", legendValueFont, labelBrush, legendArea.Left + 90, legendRowY + 1);
                legendRowY += 26;
            }
        }
        else
        {
            var gap = 10;
            var itemWidth = Math.Max(96, (legendArea.Width - gap * (counts.Length - 1)) / counts.Length);
            var itemHeight = Math.Min(legendArea.Height, 42);
            var itemTop = legendArea.Top + Math.Max(0, (legendArea.Height - itemHeight) / 2);

            for (var index = 0; index < counts.Length; index++)
            {
                var (name, value, color) = counts[index];
                var itemLeft = legendArea.Left + index * (itemWidth + gap);
                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, itemLeft, itemTop + 8, 10, 10);
                g.DrawString(name, legendTitleFont, totalBrush, itemLeft + 16, itemTop);
                g.DrawString($"{value} / {value * 100F / total:0.0}%", legendValueFont, labelBrush, itemLeft + 16, itemTop + 20);
            }
        }

        var useModernLegend = Environment.TickCount != int.MinValue;
        if (useModernLegend)
        {
            return;
        }

        var legendX = donutRect.Right + 18;
        var legendY = contentRect.Top + Math.Max(8, (contentRect.Height - 88) / 2);
        using var titleFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        using var valueSmallFont = new Font("Microsoft YaHei UI", 9F);
        foreach (var (name, value, color) in counts)
        {
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, legendX, legendY + 6, 12, 12);
            g.DrawString(name, titleFont, totalBrush, legendX + 20, legendY);
            g.DrawString($"数量：{value}", valueSmallFont, labelBrush, legendX + 20, legendY + 22);
            g.DrawString($"占比：{value * 100F / total:0.0}%", valueSmallFont, labelBrush, legendX + 20, legendY + 40);
            legendY += 66;
        }

        var compactLegendRect = new Rectangle(
            legendX - 6,
            contentRect.Top + Math.Max(0, (contentRect.Height - 94) / 2),
            Math.Min(legendWidth + 6, Math.Max(0, contentRect.Right - legendX + 6)),
            94);
        using var overlayBrush = new SolidBrush(SurfaceBackground);
        g.FillRectangle(overlayBrush, compactLegendRect);

        var compactLegendY = compactLegendRect.Top + 4;
        foreach (var (name, value, color) in counts)
        {
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, legendX, compactLegendY + 8, 12, 12);
            g.DrawString(name, titleFont, totalBrush, legendX + 20, compactLegendY + 1);
            g.DrawString($"{value}  /  {value * 100F / total:0.0}%", valueSmallFont, labelBrush, legendX + 76, compactLegendY + 3);
            compactLegendY += 30;
        }
    }

    private static void DrawTrendSeries(
        Graphics graphics,
        Rectangle plotRect,
        IReadOnlyList<InspectionTrendPointViewModel> points,
        int maxValue,
        Func<InspectionTrendPointViewModel, int> selector,
        Color color)
    {
        using var pen = new Pen(color, 2.4F);
        using var brush = new SolidBrush(color);

        var positions = new List<PointF>(points.Count);
        for (var index = 0; index < points.Count; index++)
        {
            var x = plotRect.Left + plotRect.Width * index / Math.Max(1F, points.Count - 1F);
            var ratio = selector(points[index]) / (float)maxValue;
            var y = plotRect.Bottom - plotRect.Height * ratio;
            positions.Add(new PointF(x, y));
        }

        if (positions.Count > 1)
        {
            graphics.DrawLines(pen, positions.ToArray());
        }

        foreach (var position in positions)
        {
            graphics.FillEllipse(brush, position.X - 3.5F, position.Y - 3.5F, 7, 7);
        }
    }

    private static void DrawLegend(Graphics graphics, Point origin)
    {
        DrawLegendItem(graphics, origin, "正常", SuccessColor);
        DrawLegendItem(graphics, new Point(origin.X + 54, origin.Y), "预警", WarningColor);
        DrawLegendItem(graphics, new Point(origin.X + 108, origin.Y), "异常", DangerColor);
    }

    private static void DrawLegendItem(Graphics graphics, Point origin, string text, Color color)
    {
        using var brush = new SolidBrush(color);
        using var textBrush = new SolidBrush(TextMutedColor);
        using var font = new Font("Microsoft YaHei UI", 8.5F);
        graphics.FillEllipse(brush, origin.X, origin.Y + 4, 8, 8);
        graphics.DrawString(text, font, textBrush, origin.X + 12, origin.Y);
    }

    private static void DrawCenteredText(Graphics graphics, Rectangle bounds, string text, Font font, Brush brush)
    {
        var size = graphics.MeasureString(text, font);
        graphics.DrawString(
            text,
            font,
            brush,
            bounds.Left + (bounds.Width - size.Width) / 2F,
            bounds.Top + (bounds.Height - size.Height) / 2F);
    }

    private static string BuildTrendSubtitle(IReadOnlyList<InspectionTrendPointViewModel> points)
    {
        if (points.Count == 0)
        {
            return "当前没有趋势数据";
        }

        return $"从 {points.First().Label} 到 {points.Last().Label} 的状态变化";
    }

    private static IReadOnlyList<LineSummaryRow> BuildLineRows(IReadOnlyList<InspectionRecordViewModel> records)
    {
        return records
            .GroupBy(record => record.LineName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var total = group.Count();
                var normal = group.Count(record => record.Status == InspectionStatus.Normal);
                var warning = group.Count(record => record.Status == InspectionStatus.Warning);
                var abnormal = group.Count(record => record.Status == InspectionStatus.Abnormal);
                var passRate = total == 0 ? 0 : normal * 100F / total;

                return new LineSummaryRow
                {
                    LineName = group.First().LineName,
                    TotalCount = total,
                    NormalCount = normal,
                    WarningCount = warning,
                    AbnormalCount = abnormal,
                    PassRateText = $"{passRate:0.0}%"
                };
            })
            .OrderByDescending(row => row.AbnormalCount)
            .ThenByDescending(row => row.WarningCount)
            .ThenByDescending(row => row.TotalCount)
            .ThenBy(row => row.LineName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<AttentionRow> BuildAttentionRows(IReadOnlyList<InspectionRecordViewModel> records)
    {
        return records
            .Where(record => record.Status != InspectionStatus.Normal)
            .OrderByDescending(record => record.CheckedAtValue)
            .Take(8)
            .Select(record => new AttentionRow
            {
                CheckedAt = record.CheckedAt,
                TargetName = $"{record.LineName} / {record.DeviceName}",
                StatusText = record.StatusText,
                Remark = BuildAttentionRemark(record)
            })
            .ToList();
    }

    private static string BuildAttentionRemark(InspectionRecordViewModel record)
    {
        var prefix = record.IsClosed ? "已闭环" : "待跟进";
        var detail = string.IsNullOrWhiteSpace(record.Remark) ? record.ActionRemark : record.Remark;
        return string.IsNullOrWhiteSpace(detail) ? prefix : $"{prefix} · {detail}";
    }

    private sealed class BufferedDataGridView : DataGridView
    {
        public BufferedDataGridView()
        {
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
            UpdateStyles();
        }
    }

    private sealed class LineSummaryRow
    {
        public string LineName { get; init; } = string.Empty;

        public int TotalCount { get; init; }

        public int NormalCount { get; init; }

        public int WarningCount { get; init; }

        public int AbnormalCount { get; init; }

        public string PassRateText { get; init; } = "0%";
    }

    private sealed class AttentionRow
    {
        public string CheckedAt { get; init; } = string.Empty;

        public string TargetName { get; init; } = string.Empty;

        public string StatusText { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;
    }
}
