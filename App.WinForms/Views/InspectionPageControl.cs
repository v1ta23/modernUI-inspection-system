using System.Drawing;
using System.Drawing.Drawing2D;
using App.Core.Models;
using App.WinForms.Controllers;
using App.WinForms.ViewModels;

namespace App.WinForms.Views;

internal sealed partial class InspectionPageControl : UserControl
{
    private static Color PageBackground = Color.FromArgb(10, 10, 15);
    private static Color SurfaceBackground = Color.FromArgb(28, 30, 40);
    private static Color SurfaceBorder = Color.FromArgb(80, 85, 110);
    private static Color InputBackground = Color.FromArgb(18, 22, 30);
    private static Color TextPrimaryColor = Color.FromArgb(255, 255, 255);
    private static Color TextSecondaryColor = Color.FromArgb(210, 215, 230);
    private static Color TextMutedColor = Color.FromArgb(160, 170, 190);
    private static Color AccentBlue = Color.FromArgb(88, 130, 255);

    private readonly InspectionController _controller;
    private readonly string _account;

    private readonly ComboBox _entryLineCombo;
    private readonly TextBox _entryDeviceTextBox;
    private readonly TextBox _entryItemTextBox;
    private readonly TextBox _entryInspectorTextBox;
    private readonly ComboBox _entryStatusCombo;
    private readonly NumericUpDown _entryMeasuredValueInput;
    private readonly DateTimePicker _entryCheckedAtPicker;
    private readonly TextBox _entryRemarkTextBox;
    private readonly Label _entryFeedbackLabel;

    private readonly TextBox _filterKeywordTextBox;
    private readonly ComboBox _filterLineCombo;
    private readonly ComboBox _filterStatusCombo;
    private readonly DateTimePicker _filterStartPicker;
    private readonly DateTimePicker _filterEndPicker;

    private readonly Label _refreshLabel;
    private readonly Button _toggleEntryPanelButton;
    private readonly Button _toggleChartsButton;
    private readonly Label _totalValueLabel;
    private readonly Label _normalValueLabel;
    private readonly Label _warningValueLabel;
    private readonly Label _abnormalValueLabel;
    private readonly Label _passRateValueLabel;

    private readonly DataGridView _recordsGrid;
    private readonly Panel _trendChart;
    private readonly Panel _statusChart;
    private readonly Control _layoutRoot;
    private readonly PictureBox _resizeSnapshotBox;
    private Panel? _headerCard;
    private TableLayoutPanel? _headerLayout;
    private Panel? _headerTitlePanel;
    private Panel? _headerRightPanel;
    private FlowLayoutPanel? _headerActionPanel;
    private Label? _headerTitleLabel;
    private Label? _headerSubtitleLabel;
    private TableLayoutPanel? _filterLayout;
    private Control? _filterKeywordBlock;
    private Control? _filterLineBlock;
    private Control? _filterStatusBlock;
    private Control? _filterStartBlock;
    private Control? _filterEndBlock;
    private Control? _filterActionBlock;
    private FlowLayoutPanel? _filterActionPanel;
    private InspectionDashboardViewModel _currentDashboard = new();
    private Control? _chartsPanel;
    private SplitContainer? _chartsSplitContainer;
    private Form? _entryWindow;
    private bool _isDisposingFloatingWindows;
    private bool _isInteractiveResize;
    private Bitmap? _resizeSnapshot;

    private sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
            UpdateStyles();
        }
    }

    private sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
            UpdateStyles();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool isDark = PageBackground.R < 100;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var cardPath = CreateRoundedPath(rect, 16);

            if (isDark)
            {
                using var brush = new SolidBrush(SurfaceBackground);
                using var pen = new Pen(SurfaceBorder, 1.2F);
                e.Graphics.FillPath(brush, cardPath);
                e.Graphics.DrawPath(pen, cardPath);
            }
            else
            {
                // Clean, high-contrast white cards for Light mode
                using var brush = new SolidBrush(SurfaceBackground);
                using var pen = new Pen(Color.FromArgb(226, 232, 240), 1.2F);
                e.Graphics.FillPath(brush, cardPath);
                e.Graphics.DrawPath(pen, cardPath);
            }
            
            cardPath.Dispose();
        }

        private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
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

    private sealed class StatusOption
    {
        public StatusOption(string text, InspectionStatus? value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }

        public InspectionStatus? Value { get; }

        public override string ToString() => Text;
    }

    public InspectionPageControl(string account, InspectionController controller)
    {
        _account = account;
        _controller = controller;

        Text = "点检记录中心";
        Dock = DockStyle.Fill;
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = PageBackground;
        DoubleBuffered = true;

        _entryLineCombo = CreateEditableComboBox();
        _entryDeviceTextBox = CreateTextBox();
        _entryItemTextBox = CreateTextBox();
        _entryInspectorTextBox = CreateTextBox();
        _entryStatusCombo = CreateDropDownListComboBox();
        _entryMeasuredValueInput = CreateMeasuredValueInput();
        _entryCheckedAtPicker = CreateDateTimePicker(false);
        _entryRemarkTextBox = CreateTextBox(multiline: true);
        _entryFeedbackLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = AccentBlue,
            Margin = new Padding(0, 4, 0, 0),
            Text = "录入后列表和图表会即时刷新。"
        };

        _filterKeywordTextBox = CreateTextBox();
        _filterLineCombo = CreateDropDownListComboBox();
        _filterStatusCombo = CreateDropDownListComboBox();
        _filterStartPicker = CreateDateTimePicker(true);
        _filterEndPicker = CreateDateTimePicker(true);

        _refreshLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextMutedColor,
            TextAlign = ContentAlignment.MiddleRight
        };
        _toggleEntryPanelButton = CreateSecondaryButton("新增点检");
        _toggleEntryPanelButton.Click += (_, _) => OpenEntryWindow();
        _toggleChartsButton = CreateSecondaryButton("趋势分析");
        _toggleChartsButton.Click += (_, _) => ToggleChartsDrawer();

        _totalValueLabel = CreateMetricValueLabel();
        _normalValueLabel = CreateMetricValueLabel();
        _warningValueLabel = CreateMetricValueLabel();
        _abnormalValueLabel = CreateMetricValueLabel();
        _passRateValueLabel = CreateMetricValueLabel();

        _recordsGrid = CreateRecordsGrid();
        _trendChart = CreateTrendCanvas();
        _statusChart = CreateStatusCanvas();

        _layoutRoot = BuildLayout();
        _resizeSnapshotBox = CreateResizeSnapshotBox();
        Controls.Add(_layoutRoot);
        Controls.Add(_resizeSnapshotBox);
        ApplyDarkVisualTree(this);
        Load += (_, _) => InitializeScreen();
        Disposed += (_, _) =>
        {
            DisposeResizeSnapshot();
            DisposeFloatingWindows();
        };
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_isInteractiveResize)
        {
            ApplyResponsiveLayout();
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            return;
        }

        HideEntryWindow();
        SetChartsDrawerVisible(false);
    }

    private void InitializeScreen()
    {
        BindStatusOptions();
        ResetFilters();
        ResetEntryForm();
        RefreshDashboard();
        ApplyResponsiveLayout();
    }

    public void RefreshData()
    {
        RefreshDashboard();
    }

    private void OpenEntryWindow()
    {
        _entryWindow ??= CreateEntryWindow();
        var owner = FindForm();
        CenterEntryWindow(owner);
        if (!_entryWindow.Visible)
        {
            if (owner is not null)
            {
                _entryWindow.Show(owner);
            }
            else
            {
                _entryWindow.Show();
            }
        }

        CenterEntryWindow(owner);
        _entryWindow.BringToFront();
        _entryWindow.Activate();
        if (_entryLineCombo.CanFocus)
        {
            _entryLineCombo.Focus();
        }
    }

    private Form CreateEntryWindow()
    {
        var window = new Form
        {
            Text = "点检录入",
            StartPosition = FormStartPosition.Manual,
            Size = new Size(640, 900),
            MinimumSize = new Size(600, 820),
            FormBorderStyle = FormBorderStyle.SizableToolWindow,
            BackColor = PageBackground,
            Font = Font,
            ShowIcon = false,
            ShowInTaskbar = false
        };

        var shell = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            Padding = new Padding(18)
        };
        shell.Controls.Add(BuildEntryPanel());
        window.Controls.Add(shell);
        ApplyDarkVisualTree(window);
        window.FormClosing += (_, args) =>
        {
            if (args.CloseReason == CloseReason.UserClosing)
            {
                args.Cancel = true;
                window.Hide();
            }
        };
        return window;
    }

    private void CenterEntryWindow(Form? owner)
    {
        if (_entryWindow is null)
        {
            return;
        }

        var preferredBounds = owner?.Bounds ?? Screen.FromControl(this).WorkingArea;
        var workingArea = owner is not null
            ? Screen.FromControl(owner).WorkingArea
            : Screen.FromControl(this).WorkingArea;
        var x = preferredBounds.Left + Math.Max(0, (preferredBounds.Width - _entryWindow.Width) / 2);
        var y = preferredBounds.Top + Math.Max(0, (preferredBounds.Height - _entryWindow.Height) / 2);
        x = Math.Clamp(x, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - _entryWindow.Width));
        y = Math.Clamp(y, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - _entryWindow.Height));
        _entryWindow.Location = new Point(x, y);
    }

    private void HideEntryWindow()
    {
        if (_entryWindow?.Visible == true)
        {
            _entryWindow.Hide();
        }
    }

    private void DisposeFloatingWindows()
    {
        _isDisposingFloatingWindows = true;
        try
        {
            if (_entryWindow is not null && !_entryWindow.IsDisposed)
            {
                _entryWindow.Dispose();
            }
        }
        finally
        {
            _entryWindow = null;
            _isDisposingFloatingWindows = false;
        }
    }

    private void ToggleChartsDrawer()
    {
        SetChartsDrawerVisible(_chartsPanel?.Visible != true);
    }

    private void SetChartsDrawerVisible(bool visible)
    {
        if (_chartsPanel is null)
        {
            return;
        }

        _chartsPanel.Visible = visible;
        _toggleChartsButton.Text = visible ? "关闭趋势" : "趋势分析";
        if (visible)
        {
            UpdateChartsSplitDistance();
            _trendChart.Invalidate();
            _statusChart.Invalidate();
        }
    }

    public void BeginInteractiveResize()
    {
        if (_isInteractiveResize)
        {
            return;
        }

        _isInteractiveResize = true;
        SuspendGridAutosize();
        CaptureResizeSnapshot();
        _layoutRoot.SuspendLayout();
        _layoutRoot.Visible = false;
        _resizeSnapshotBox.Visible = true;
        _resizeSnapshotBox.BringToFront();
    }

    public void EndInteractiveResize()
    {
        if (!_isInteractiveResize)
        {
            return;
        }

        _isInteractiveResize = false;
        _resizeSnapshotBox.Visible = false;
        _layoutRoot.Visible = true;
        _layoutRoot.ResumeLayout(true);
        DisposeResizeSnapshot();
        ResumeGridAutosize();
        ApplyResponsiveLayout();
        if (_chartsPanel?.Visible == true)
        {
            _trendChart.Invalidate();
            _statusChart.Invalidate();
        }

        Invalidate();
    }

    private void ApplyResponsiveLayout()
    {
        UpdateHeaderLayout();
        UpdateFilterLayout();
        UpdateChartsSplitDistance();
    }

    private void UpdateHeaderLayout()
    {
        if (_headerCard is null ||
            _headerLayout is null ||
            _headerTitlePanel is null ||
            _headerRightPanel is null ||
            _headerActionPanel is null ||
            _headerTitleLabel is null ||
            _headerSubtitleLabel is null)
        {
            return;
        }

        var stacked = ClientSize.Width < 1080;
        _headerCard.Height = stacked ? 150 : 112;

        _headerLayout.SuspendLayout();
        _headerLayout.Controls.Clear();
        _headerLayout.ColumnStyles.Clear();
        _headerLayout.RowStyles.Clear();

        if (stacked)
        {
            _headerLayout.ColumnCount = 1;
            _headerLayout.RowCount = 2;
            _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _headerLayout.Controls.Add(_headerTitlePanel, 0, 0);
            _headerLayout.Controls.Add(_headerRightPanel, 0, 1);
            _headerRightPanel.Margin = new Padding(0, 10, 0, 0);
            _headerActionPanel.FlowDirection = FlowDirection.LeftToRight;
            _refreshLabel.TextAlign = ContentAlignment.MiddleLeft;
        }
        else
        {
            _headerLayout.ColumnCount = 2;
            _headerLayout.RowCount = 1;
            _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360F));
            _headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _headerLayout.Controls.Add(_headerTitlePanel, 0, 0);
            _headerLayout.Controls.Add(_headerRightPanel, 1, 0);
            _headerRightPanel.Margin = Padding.Empty;
            _headerActionPanel.FlowDirection = FlowDirection.RightToLeft;
            _refreshLabel.TextAlign = ContentAlignment.MiddleRight;
        }

        var availableTitleWidth = Math.Max(200, _headerTitlePanel.ClientSize.Width);
        _headerTitleLabel.MaximumSize = new Size(availableTitleWidth, 0);
        _headerSubtitleLabel.MaximumSize = new Size(availableTitleWidth, 0);
        _headerLayout.ResumeLayout(true);
    }

    private void UpdateFilterLayout()
    {
        if (_filterLayout is null ||
            _filterKeywordBlock is null ||
            _filterLineBlock is null ||
            _filterStatusBlock is null ||
            _filterStartBlock is null ||
            _filterEndBlock is null ||
            _filterActionBlock is null)
        {
            return;
        }

        var availableWidth = _filterLayout.Parent?.ClientSize.Width ?? ClientSize.Width;
        var columns = availableWidth >= 1180
            ? 4
            : availableWidth >= 820
                ? 3
                : availableWidth >= 560
                    ? 2
                    : 1;

        var filterBlocks = new[]
        {
            _filterKeywordBlock,
            _filterLineBlock,
            _filterStatusBlock,
            _filterStartBlock,
            _filterEndBlock
        };

        _filterLayout.SuspendLayout();
        _filterLayout.Controls.Clear();
        _filterLayout.ColumnStyles.Clear();
        _filterLayout.RowStyles.Clear();
        _filterLayout.ColumnCount = columns;

        for (var column = 0; column < columns; column++)
        {
            _filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
        }

        var row = 0;
        var columnIndex = 0;
        foreach (var block in filterBlocks)
        {
            _filterLayout.Controls.Add(block, columnIndex, row);
            columnIndex++;
            if (columnIndex < columns)
            {
                continue;
            }

            columnIndex = 0;
            row++;
        }

        if (columnIndex != 0)
        {
            row++;
        }

        _filterLayout.Controls.Add(_filterActionBlock, 0, row);
        _filterLayout.SetColumnSpan(_filterActionBlock, columns);
        _filterLayout.RowCount = row + 1;

        for (var rowIndex = 0; rowIndex < _filterLayout.RowCount; rowIndex++)
        {
            _filterLayout.RowStyles.Add(new RowStyle());
        }

        if (_filterActionPanel is not null)
        {
            _filterActionPanel.FlowDirection = FlowDirection.LeftToRight;
        }

        _filterLayout.ResumeLayout(true);

        if (_filterActionPanel is not null &&
            _filterActionBlock.Width > 0 &&
            _filterActionPanel.GetPreferredSize(Size.Empty).Width > _filterActionBlock.Width)
        {
            _filterLayout.SuspendLayout();
            _filterActionPanel.FlowDirection = FlowDirection.TopDown;
            _filterLayout.ResumeLayout(true);
        }
    }

    private void UpdateChartsSplitDistance()
    {
        if (_chartsSplitContainer is null || _chartsPanel?.Visible != true)
        {
            return;
        }

        var height = _chartsSplitContainer.Height;
        if (height <= 0)
        {
            return;
        }

        var minAllowed = _chartsSplitContainer.Panel1MinSize;
        var maxAllowed = height - _chartsSplitContainer.Panel2MinSize - 1;
        if (maxAllowed <= minAllowed)
        {
            return;
        }

        var preferredMin = Math.Min(320, Math.Max(240, height / 3));
        var preferredMax = Math.Max(preferredMin, height - 240);
        var minDistance = Math.Max(minAllowed, Math.Min(preferredMin, maxAllowed));
        var maxDistance = Math.Max(minDistance, Math.Min(preferredMax, maxAllowed));
        var desired = Math.Clamp((int)(height * 0.56F), minDistance, maxDistance);
        if (Math.Abs(_chartsSplitContainer.SplitterDistance - desired) > 8)
        {
            _chartsSplitContainer.SplitterDistance = desired;
        }
    }

    private void BindStatusOptions()
    {
        _entryStatusCombo.Items.Clear();
        _entryStatusCombo.Items.Add(new StatusOption("正常", InspectionStatus.Normal));
        _entryStatusCombo.Items.Add(new StatusOption("预警", InspectionStatus.Warning));
        _entryStatusCombo.Items.Add(new StatusOption("异常", InspectionStatus.Abnormal));
        _entryStatusCombo.SelectedIndex = 0;

        _filterStatusCombo.Items.Clear();
        _filterStatusCombo.Items.Add(new StatusOption("全部", null));
        _filterStatusCombo.Items.Add(new StatusOption("正常", InspectionStatus.Normal));
        _filterStatusCombo.Items.Add(new StatusOption("预警", InspectionStatus.Warning));
        _filterStatusCombo.Items.Add(new StatusOption("异常", InspectionStatus.Abnormal));
        _filterStatusCombo.SelectedIndex = 0;
    }

    private void RefreshDashboard()
    {
        try
        {
            var dashboard = _controller.Load(BuildFilter());
            UpdateLineOptions(dashboard.LineOptions);
            UpdateSummary(dashboard);
            UpdateGrid(dashboard.Records);
            UpdateCharts(dashboard);
            _refreshLabel.Text = $"最近刷新：{dashboard.GeneratedAt:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "查询失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void UpdateLineOptions(IReadOnlyList<string> lineOptions)
    {
        var filterCurrent = _filterLineCombo.SelectedItem?.ToString();
        var entryCurrent = _entryLineCombo.Text;

        _filterLineCombo.BeginUpdate();
        _filterLineCombo.Items.Clear();
        _filterLineCombo.Items.Add("全部");
        foreach (var line in lineOptions)
        {
            _filterLineCombo.Items.Add(line);
        }
        _filterLineCombo.EndUpdate();
        _filterLineCombo.SelectedItem = !string.IsNullOrWhiteSpace(filterCurrent) && _filterLineCombo.Items.Contains(filterCurrent)
            ? filterCurrent
            : _filterLineCombo.Items[0];

        _entryLineCombo.BeginUpdate();
        _entryLineCombo.Items.Clear();
        foreach (var line in lineOptions)
        {
            _entryLineCombo.Items.Add(line);
        }
        _entryLineCombo.EndUpdate();

        if (!string.IsNullOrWhiteSpace(entryCurrent))
        {
            _entryLineCombo.Text = entryCurrent;
        }
        else if (_entryLineCombo.Items.Count > 0)
        {
            _entryLineCombo.SelectedIndex = 0;
        }
    }

    private void UpdateSummary(InspectionDashboardViewModel dashboard)
    {
        _totalValueLabel.Text = dashboard.TotalCount.ToString();
        _normalValueLabel.Text = dashboard.NormalCount.ToString();
        _warningValueLabel.Text = dashboard.WarningCount.ToString();
        _abnormalValueLabel.Text = dashboard.AbnormalCount.ToString();
        _passRateValueLabel.Text = dashboard.PassRateText;
    }

    private void UpdateGrid(IReadOnlyList<InspectionRecordViewModel> records)
    {
        _recordsGrid.DataSource = records.ToList();
    }

    private void UpdateCharts(InspectionDashboardViewModel dashboard)
    {
        _currentDashboard = dashboard;
        _trendChart.Invalidate();
        _statusChart.Invalidate();
    }

    private void SuspendGridAutosize()
    {
    }

    private void ResumeGridAutosize()
    {
    }

    private PictureBox CreateResizeSnapshotBox()
    {
        var box = new PictureBox
        {
            BackColor = PageBackground,
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.StretchImage,
            Visible = false
        };

        box.Paint += (_, e) =>
        {
            if (box.Image is not null)
            {
                return;
            }

            using var titleFont = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold);
            using var subtitleFont = new Font("Microsoft YaHei UI", 9.5F);
            using var titleBrush = new SolidBrush(TextPrimaryColor);
            using var subtitleBrush = new SolidBrush(TextMutedColor);

            var titleText = "Resizing window...";
            var subtitleText = "Layout and charts will refresh after resize.";
            var titleSize = e.Graphics.MeasureString(titleText, titleFont);
            var subtitleSize = e.Graphics.MeasureString(subtitleText, subtitleFont);
            var centerX = box.ClientSize.Width / 2F;
            var centerY = box.ClientSize.Height / 2F;

            e.Graphics.DrawString(titleText, titleFont, titleBrush, centerX - titleSize.Width / 2F, centerY - 24F);
            e.Graphics.DrawString(subtitleText, subtitleFont, subtitleBrush, centerX - subtitleSize.Width / 2F, centerY + 8F);
        };

        return box;
    }

    private void CaptureResizeSnapshot()
    {
        DisposeResizeSnapshot();
        if (_layoutRoot.Width <= 0 || _layoutRoot.Height <= 0)
        {
            return;
        }

        try
        {
            _resizeSnapshot = new Bitmap(_layoutRoot.Width, _layoutRoot.Height);
            _layoutRoot.DrawToBitmap(_resizeSnapshot, new Rectangle(Point.Empty, _layoutRoot.Size));
            _resizeSnapshotBox.Image = _resizeSnapshot;
        }
        catch
        {
            DisposeResizeSnapshot();
        }
    }

    private void DisposeResizeSnapshot()
    {
        _resizeSnapshotBox.Image = null;
        _resizeSnapshot?.Dispose();
        _resizeSnapshot = null;
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var entry = BuildEntry();
            _controller.Add(entry);
            ResetEntryForm(keepLine: true, keepInspector: true);
            _entryFeedbackLabel.ForeColor = Color.FromArgb(39, 174, 96);
            _entryFeedbackLabel.Text = $"已保存：{entry.DeviceName} / {entry.CheckedAt:yyyy-MM-dd HH:mm}";
            RefreshDashboard();
        }
        catch (Exception ex)
        {
            _entryFeedbackLabel.ForeColor = Color.FromArgb(231, 76, 60);
            _entryFeedbackLabel.Text = ex.Message;
        }
    }

    private void OnExportClicked(object? sender, EventArgs e)
    {
        try
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
                FileName = $"点检记录_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                RestoreDirectory = true
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _controller.Export(dialog.FileName, BuildFilter());
            MessageBox.Show(this, $"导出完成：{dialog.FileName}", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private InspectionFilterViewModel BuildFilter()
    {
        var startTime = _filterStartPicker.Checked ? (DateTime?)_filterStartPicker.Value : null;
        var endTime = _filterEndPicker.Checked ? (DateTime?)_filterEndPicker.Value : null;

        if (startTime.HasValue && endTime.HasValue && startTime > endTime)
        {
            throw new InvalidOperationException("开始时间不能大于结束时间。");
        }

        var selectedLine = _filterLineCombo.SelectedItem?.ToString();
        var status = (_filterStatusCombo.SelectedItem as StatusOption)?.Value;

        return new InspectionFilterViewModel
        {
            Keyword = _filterKeywordTextBox.Text.Trim(),
            LineName = selectedLine == "全部" ? string.Empty : selectedLine ?? string.Empty,
            Status = status,
            StartTime = startTime,
            EndTime = endTime
        };
    }

    private InspectionEntryViewModel BuildEntry()
    {
        return new InspectionEntryViewModel
        {
            LineName = _entryLineCombo.Text.Trim(),
            DeviceName = _entryDeviceTextBox.Text.Trim(),
            InspectionItem = _entryItemTextBox.Text.Trim(),
            Inspector = _entryInspectorTextBox.Text.Trim(),
            Status = (_entryStatusCombo.SelectedItem as StatusOption)?.Value ?? InspectionStatus.Normal,
            MeasuredValue = _entryMeasuredValueInput.Value,
            CheckedAt = _entryCheckedAtPicker.Value,
            Remark = _entryRemarkTextBox.Text.Trim()
        };
    }

    private void ResetFilters()
    {
        _filterKeywordTextBox.Clear();
        _filterStartPicker.Checked = false;
        _filterEndPicker.Checked = false;
        if (_filterLineCombo.Items.Count > 0)
        {
            _filterLineCombo.SelectedIndex = 0;
        }
        if (_filterStatusCombo.Items.Count > 0)
        {
            _filterStatusCombo.SelectedIndex = 0;
        }
    }

    private void ResetEntryForm(bool keepLine = false, bool keepInspector = false)
    {
        var currentLine = _entryLineCombo.Text;
        var currentInspector = _entryInspectorTextBox.Text;

        if (keepLine)
        {
            _entryLineCombo.Text = currentLine;
        }
        else if (_entryLineCombo.Items.Count > 0)
        {
            _entryLineCombo.SelectedIndex = 0;
        }
        else
        {
            _entryLineCombo.Text = string.Empty;
        }

        _entryDeviceTextBox.Clear();
        _entryItemTextBox.Clear();
        _entryInspectorTextBox.Text = keepInspector ? currentInspector : string.Empty;
        _entryStatusCombo.SelectedIndex = 0;
        _entryMeasuredValueInput.Value = 0;
        _entryCheckedAtPicker.Value = DateTime.Now;
        _entryRemarkTextBox.Clear();

        if (!keepLine && !keepInspector)
        {
            _entryFeedbackLabel.ForeColor = Color.FromArgb(83, 131, 255);
            _entryFeedbackLabel.Text = "录入后列表和图表会即时刷新。";
        }
    }

    public void ApplyTheme(bool isDarkTheme)
    {
        if (isDarkTheme)
        {
            PageBackground = Color.FromArgb(10, 10, 15);
            SurfaceBackground = Color.FromArgb(28, 30, 40);
            SurfaceBorder = Color.FromArgb(80, 85, 110);
            InputBackground = Color.FromArgb(18, 22, 30);
            TextPrimaryColor = Color.FromArgb(255, 255, 255);
            TextSecondaryColor = Color.FromArgb(210, 215, 230);
            TextMutedColor = Color.FromArgb(160, 170, 190);
        }
        else
        {
            PageBackground = Color.FromArgb(214, 226, 240);
            SurfaceBackground = Color.FromArgb(255, 255, 255);
            SurfaceBorder = Color.FromArgb(210, 215, 225);
            InputBackground = Color.FromArgb(248, 250, 252);
            TextPrimaryColor = Color.FromArgb(31, 41, 55);
            TextSecondaryColor = Color.FromArgb(99, 114, 130);
            TextMutedColor = Color.FromArgb(144, 155, 170);
        }

        BackColor = PageBackground;
        if (_trendChart != null) _trendChart.BackColor = SurfaceBackground;
        if (_statusChart != null) _statusChart.BackColor = SurfaceBackground;
        
        if (_layoutRoot != null) ApplyDarkVisualTree(_layoutRoot);
        if (_chartsPanel != null) ApplyDarkVisualTree(_chartsPanel);
        if (_entryWindow != null)
        {
            _entryWindow.BackColor = PageBackground;
            ApplyDarkVisualTree(_entryWindow);
        }
        Invalidate(true);
    }

    private void ApplyDarkVisualTree(Control root)
    {
        bool isDarkTheme = PageBackground.R < 100;

        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case CardPanel cardPanel:
                    cardPanel.BackColor = PageBackground;
                    break;
                case Panel panel when panel is not CardPanel && panel.Parent is CardPanel:
                    panel.BackColor = SurfaceBackground;
                    break;
                case TableLayoutPanel tlp when tlp.Parent is Panel && tlp.Parent.Parent is CardPanel:
                    tlp.BackColor = SurfaceBackground;
                    break;
                case Label label when label == _totalValueLabel:
                    label.BackColor = SurfaceBackground;
                    label.ForeColor = isDarkTheme ? Color.FromArgb(83, 131, 255) : Color.FromArgb(41, 98, 255);
                    break;
                case Label label when label == _normalValueLabel:
                    label.BackColor = SurfaceBackground;
                    label.ForeColor = isDarkTheme ? Color.FromArgb(39, 174, 96) : Color.FromArgb(22, 138, 62);
                    break;
                case Label label when label == _warningValueLabel:
                    label.BackColor = SurfaceBackground;
                    label.ForeColor = isDarkTheme ? Color.FromArgb(241, 196, 15) : Color.FromArgb(217, 119, 6);
                    break;
                case Label label when label == _abnormalValueLabel:
                    label.BackColor = SurfaceBackground;
                    label.ForeColor = isDarkTheme ? Color.FromArgb(231, 76, 60) : Color.FromArgb(220, 38, 38);
                    break;
                case Label label when label == _passRateValueLabel:
                    label.BackColor = SurfaceBackground;
                    label.ForeColor = isDarkTheme ? Color.FromArgb(52, 152, 219) : Color.FromArgb(37, 99, 235);
                    break;
                case Label label when label == _entryFeedbackLabel:
                    label.BackColor = SurfaceBackground; // Usually inside entry CardPanel
                    label.ForeColor = isDarkTheme ? Color.FromArgb(83, 131, 255) : Color.FromArgb(41, 98, 255);
                    break;
                case Label label when label == _refreshLabel:
                    label.BackColor = SurfaceBackground;
                    label.ForeColor = TextMutedColor;
                    break;
                case Label label:
                    label.BackColor = SurfaceBackground;
                    label.ForeColor = label.Font.Bold || label.Font.Size >= 12F
                        ? TextPrimaryColor
                        : TextSecondaryColor;
                    break;
                case TextBox textBox:
                    textBox.BackColor = InputBackground;
                    textBox.ForeColor = TextPrimaryColor;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = InputBackground;
                    comboBox.ForeColor = TextPrimaryColor;
                    break;
                case NumericUpDown numericUpDown:
                    numericUpDown.BackColor = InputBackground;
                    numericUpDown.ForeColor = TextPrimaryColor;
                    break;
                case DateTimePicker dateTimePicker:
                    dateTimePicker.CalendarForeColor = TextPrimaryColor;
                    dateTimePicker.CalendarMonthBackground = InputBackground;
                    dateTimePicker.CalendarTitleBackColor = SurfaceBackground;
                    dateTimePicker.CalendarTitleForeColor = TextPrimaryColor;
                    break;
                case Button button:
                    if (button.FlatAppearance.BorderSize == 0) // Primary button
                    {
                        button.BackColor = AccentBlue;
                        button.ForeColor = Color.White;
                    }
                    else // Secondary button
                    {
                        button.BackColor = InputBackground;
                        button.ForeColor = TextSecondaryColor;
                        button.FlatAppearance.BorderColor = SurfaceBorder;
                    }
                    break;
                case DataGridView grid:
                    grid.BackgroundColor = SurfaceBackground;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = InputBackground;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimaryColor;
                    grid.DefaultCellStyle.BackColor = SurfaceBackground;
                    grid.DefaultCellStyle.ForeColor = TextSecondaryColor;
                    grid.DefaultCellStyle.SelectionBackColor = isDarkTheme ? Color.FromArgb(45, 56, 78) : Color.FromArgb(226, 232, 240);
                    grid.DefaultCellStyle.SelectionForeColor = TextPrimaryColor;
                    grid.GridColor = SurfaceBorder;
                    break;
            }

            ApplyDarkVisualTree(control);
        }

        _entryFeedbackLabel.ForeColor = AccentBlue;
        _refreshLabel.ForeColor = TextMutedColor;
    }

    private static TextBox CreateTextBox(bool multiline = false)
    {
        return new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = InputBackground,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            ForeColor = TextPrimaryColor,
            Multiline = multiline,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
        };
    }

    private static ComboBox CreateDropDownListComboBox()
    {
        return new ComboBox
        {
            BackColor = InputBackground,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            ForeColor = TextPrimaryColor,
            IntegralHeight = false
        };
    }

    private static ComboBox CreateEditableComboBox()
    {
        return new ComboBox
        {
            BackColor = InputBackground,
            DropDownStyle = ComboBoxStyle.DropDown,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            ForeColor = TextPrimaryColor,
            IntegralHeight = false
        };
    }

    private static NumericUpDown CreateMeasuredValueInput()
    {
        return new NumericUpDown
        {
            BackColor = InputBackground,
            DecimalPlaces = 2,
            Maximum = 100000,
            Minimum = 0,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            ForeColor = TextPrimaryColor,
            ThousandsSeparator = true
        };
    }

    private static DateTimePicker CreateDateTimePicker(bool allowEmpty)
    {
        return new DateTimePicker
        {
            CalendarFont = new Font("Microsoft YaHei UI", 9F),
            CalendarForeColor = TextPrimaryColor,
            CalendarMonthBackground = InputBackground,
            CalendarTitleBackColor = SurfaceBackground,
            CalendarTitleForeColor = TextPrimaryColor,
            CustomFormat = "yyyy-MM-dd HH:mm",
            Format = DateTimePickerFormat.Custom,
            ShowCheckBox = allowEmpty,
            Width = 200
        };
    }

    private static Button CreatePrimaryButton(string text)
    {
        var button = new Button
        {
            AutoSize = true,
            BackColor = AccentBlue,
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextPrimaryColor,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(14, 6, 14, 6),
            Text = text,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static Button CreateSecondaryButton(string text)
    {
        var button = new Button
        {
            AutoSize = true,
            BackColor = InputBackground,
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextSecondaryColor,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(14, 6, 14, 6),
            Text = text,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = SurfaceBorder;
        return button;
    }

    private static DataGridView CreateRecordsGrid()
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
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            ScrollBars = ScrollBars.Both,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        grid.ColumnHeadersDefaultCellStyle.BackColor = InputBackground;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimaryColor;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        grid.EnableHeadersVisualStyles = false;
        grid.DefaultCellStyle.BackColor = SurfaceBackground;
        grid.DefaultCellStyle.ForeColor = TextSecondaryColor;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 56, 78);
        grid.DefaultCellStyle.SelectionForeColor = TextPrimaryColor;
        grid.GridColor = SurfaceBorder;

        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.CheckedAt), HeaderText = "点检时间", FillWeight = 120 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.LineName), HeaderText = "产线", FillWeight = 80 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.DeviceName), HeaderText = "设备名称", FillWeight = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.InspectionItem), HeaderText = "点检项目", FillWeight = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.Inspector), HeaderText = "点检人", FillWeight = 80 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.StatusText), HeaderText = "状态", FillWeight = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.MeasuredValueText), HeaderText = "测量值", FillWeight = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.Remark), HeaderText = "备注", FillWeight = 150 });

        var columnWidths = new[] { 150, 90, 160, 150, 100, 90, 100, 220 };
        for (var index = 0; index < grid.Columns.Count && index < columnWidths.Length; index++)
        {
            grid.Columns[index].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns[index].Width = columnWidths[index];
        }

        grid.CellFormatting += (_, args) =>
        {
            if (grid.Columns[args.ColumnIndex].DataPropertyName != nameof(InspectionRecordViewModel.StatusText) ||
                args.Value is not string statusText)
            {
                return;
            }

            var cellStyle = args.CellStyle;
            if (cellStyle is null)
            {
                return;
            }

            cellStyle.ForeColor = statusText switch
            {
                "正常" => grid.BackgroundColor.R < 100 ? Color.FromArgb(39, 174, 96) : Color.FromArgb(22, 138, 62),
                "预警" => grid.BackgroundColor.R < 100 ? Color.FromArgb(241, 196, 15) : Color.FromArgb(217, 119, 6),
                "异常" => grid.BackgroundColor.R < 100 ? Color.FromArgb(231, 76, 60) : Color.FromArgb(220, 38, 38),
                _ => TextPrimaryColor
            };
        };

        return grid;
    }

    private static Panel CreateTrendCanvas()
    {
        var panel = new BufferedPanel
        {
            BackColor = SurfaceBackground
        };
        panel.Paint += DrawTrendCanvas;
        return panel;
    }

    private static Panel CreateStatusCanvas()
    {
        var panel = new BufferedPanel
        {
            BackColor = SurfaceBackground
        };
        panel.Paint += DrawStatusCanvas;
        return panel;
    }

    private static InspectionPageControl? FindOwnerControl(Control? control)
    {
        while (control is not null && control is not InspectionPageControl)
        {
            control = control.Parent;
        }

        return control as InspectionPageControl;
    }

    private static void DrawCenteredHint(Graphics graphics, Rectangle bounds, string text, Font font, Brush brush)
    {
        var size = graphics.MeasureString(text, font);
        graphics.DrawString(
            text,
            font,
            brush,
            bounds.Left + (bounds.Width - size.Width) / 2F,
            bounds.Top + (bounds.Height - size.Height) / 2F);
    }

    private static void DrawTrendCanvas(object? sender, PaintEventArgs e)
    {
        if (sender is not BufferedPanel panel)
        {
            return;
        }

        var form = FindOwnerControl(panel);
        if (form is null)
        {
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(SurfaceBackground);

        var points = form._currentDashboard.TrendPoints;
        var rect = new Rectangle(20, 18, Math.Max(0, panel.Width - 40), Math.Max(0, panel.Height - 36));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        bool isDark = PageBackground.R < 100;
        using var axisPen = new Pen(SurfaceBorder, 1);
        using var gridPen = new Pen(isDark ? Color.FromArgb(55, 62, 80) : Color.FromArgb(220, 225, 235), 1);
        using var labelBrush = new SolidBrush(TextMutedColor);
        using var labelFont = new Font("Microsoft YaHei UI", 8.5F);

        if (points.Count == 0)
        {
            var emptyText = "暂无趋势数据";
            var size = g.MeasureString(emptyText, labelFont);
            g.DrawString(emptyText, labelFont, labelBrush, (panel.Width - size.Width) / 2, (panel.Height - size.Height) / 2);
            return;
        }

        var plotRect = new Rectangle(rect.X + 32, rect.Y + 18, rect.Width - 52, rect.Height - 56);
        if (plotRect.Width <= 20 || plotRect.Height <= 20)
        {
            return;
        }

        var maxValue = Math.Max(1, points.Max(point => Math.Max(point.NormalCount, Math.Max(point.WarningCount, point.AbnormalCount))));
        for (var i = 0; i <= 4; i++)
        {
            var y = plotRect.Bottom - (plotRect.Height * i / 4f);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            var axisText = Math.Round(maxValue * i / 4f).ToString("0");
            g.DrawString(axisText, labelFont, labelBrush, rect.X, y - 8);
        }

        g.DrawLine(axisPen, plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom);
        g.DrawLine(axisPen, plotRect.Left, plotRect.Top, plotRect.Left, plotRect.Bottom);

        DrawTrendSeries(g, plotRect, points, maxValue, point => point.NormalCount, Color.FromArgb(39, 174, 96));
        DrawTrendSeries(g, plotRect, points, maxValue, point => point.WarningCount, Color.FromArgb(241, 196, 15));
        DrawTrendSeries(g, plotRect, points, maxValue, point => point.AbnormalCount, Color.FromArgb(231, 76, 60));

        for (var index = 0; index < points.Count; index++)
        {
            var x = plotRect.Left + (plotRect.Width * index / Math.Max(1f, points.Count - 1f));
            var label = points[index].Label;
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, labelBrush, x - size.Width / 2, plotRect.Bottom + 10);
        }

        DrawTrendLegend(g, new Rectangle(plotRect.Right - 165, rect.Y - 2, 160, 18));
    }

    private static void DrawTrendSeries(
        Graphics graphics,
        Rectangle plotRect,
        IReadOnlyList<InspectionTrendPointViewModel> points,
        int maxValue,
        Func<InspectionTrendPointViewModel, int> selector,
        Color color)
    {
        using var seriesPen = new Pen(color, 2.6F);
        using var pointBrush = new SolidBrush(color);

        var positions = new List<PointF>();
        for (var index = 0; index < points.Count; index++)
        {
            var x = plotRect.Left + (plotRect.Width * index / Math.Max(1f, points.Count - 1f));
            var ratio = selector(points[index]) / (float)maxValue;
            var y = plotRect.Bottom - plotRect.Height * ratio;
            positions.Add(new PointF(x, y));
        }

        if (positions.Count > 1)
        {
            graphics.DrawLines(seriesPen, positions.ToArray());
        }

        foreach (var position in positions)
        {
            graphics.FillEllipse(pointBrush, position.X - 3.5F, position.Y - 3.5F, 7, 7);
        }
    }

    private static void DrawTrendLegend(Graphics graphics, Rectangle rect)
    {
        DrawLegendItem(graphics, new Point(rect.Left, rect.Top), "正常", Color.FromArgb(39, 174, 96));
        DrawLegendItem(graphics, new Point(rect.Left + 52, rect.Top), "预警", Color.FromArgb(241, 196, 15));
        DrawLegendItem(graphics, new Point(rect.Left + 104, rect.Top), "异常", Color.FromArgb(231, 76, 60));
    }

    private static void DrawLegendItem(Graphics graphics, Point origin, string text, Color color)
    {
        using var brush = new SolidBrush(color);
        using var textBrush = new SolidBrush(TextMutedColor);
        using var font = new Font("Microsoft YaHei UI", 8.5F);
        graphics.FillEllipse(brush, origin.X, origin.Y + 3, 8, 8);
        graphics.DrawString(text, font, textBrush, origin.X + 12, origin.Y);
    }

    private static void DrawStatusCanvas(object? sender, PaintEventArgs e)
    {
        if (sender is not BufferedPanel panel)
        {
            return;
        }

        var form = FindOwnerControl(panel);
        if (form is null)
        {
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(SurfaceBackground);
        using var hintBrush = new SolidBrush(TextMutedColor);
        using var hintFont = new Font("Microsoft YaHei UI", 8.5F);

        var counts = new[]
        {
            ("正常", form._currentDashboard.NormalCount, Color.FromArgb(39, 174, 96)),
            ("预警", form._currentDashboard.WarningCount, Color.FromArgb(241, 196, 15)),
            ("异常", form._currentDashboard.AbnormalCount, Color.FromArgb(231, 76, 60))
        };

        var total = counts.Sum(item => item.Item2);
        var diameter = Math.Min(panel.Width - 80, panel.Height - 70);
        diameter = Math.Max(80, diameter);
        var donutRect = new Rectangle(24, Math.Max(20, (panel.Height - diameter) / 2), diameter, diameter);

        bool isDark = PageBackground.R < 100;
        using var backPen = new Pen(isDark ? Color.FromArgb(55, 62, 80) : Color.FromArgb(215, 220, 232), 24);
        g.DrawArc(backPen, donutRect, 0, 360);

        if (total > 0)
        {
            var startAngle = -90F;
            foreach (var (name, value, color) in counts)
            {
                if (value == 0)
                {
                    continue;
                }

                var sweepAngle = 360F * value / total;
                using var pen = new Pen(color, 24);
                g.DrawArc(pen, donutRect, startAngle, sweepAngle);
                startAngle += sweepAngle;
            }
        }

        using var centerBrush = new SolidBrush(TextPrimaryColor);
        using var centerValueFont = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold);
        using var centerTextFont = new Font("Microsoft YaHei UI", 8.5F);
        var totalText = total.ToString();
        var totalSize = g.MeasureString(totalText, centerValueFont);
        g.DrawString(totalText, centerValueFont, centerBrush,
            donutRect.Left + donutRect.Width / 2F - totalSize.Width / 2,
            donutRect.Top + donutRect.Height / 2F - 22);
        g.DrawString("总记录", centerTextFont, new SolidBrush(Color.FromArgb(99, 114, 130)),
            donutRect.Left + donutRect.Width / 2F - 22,
            donutRect.Top + donutRect.Height / 2F + 6);

        var legendX = donutRect.Right + 24;
        var legendY = donutRect.Top + 8;
        foreach (var (name, value, color) in counts)
        {
            using var brush = new SolidBrush(color);
            using var titleBrush = new SolidBrush(TextPrimaryColor);
            using var valueBrush = new SolidBrush(TextMutedColor);
            using var titleFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            using var valueFont = new Font("Microsoft YaHei UI", 9F);
            g.FillRectangle(brush, legendX, legendY + 5, 12, 12);
            g.DrawString(name, titleFont, titleBrush, legendX + 20, legendY);
            g.DrawString($"数量：{value}", valueFont, valueBrush, legendX + 20, legendY + 22);
            legendY += 54;
        }
    }
}
