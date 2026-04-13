using App.Core.Models;
using Microsoft.VisualBasic.FileIO;
using System.Data;
using System.Text;
using WinFormsApp.Controllers;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Views;

internal sealed class DataInsightPageControl : UserControl, IInteractiveResizeAware
{
    private const int PreviewLimit = 200;

    private static readonly Color PageBackground = PageChrome.PageBackground;
    private static readonly Color SurfaceBackground = PageChrome.SurfaceBackground;
    private static readonly Color SurfaceBorder = PageChrome.SurfaceBorder;
    private static readonly Color InputBackground = PageChrome.InputBackground;
    private static readonly Color TextPrimaryColor = PageChrome.TextPrimary;
    private static readonly Color TextMutedColor = PageChrome.TextMuted;
    private static readonly Color AccentBlue = PageChrome.AccentBlue;
    private static readonly Color AccentGreen = PageChrome.AccentGreen;
    private static readonly Color AccentOrange = PageChrome.AccentOrange;
    private static readonly Color AccentRed = PageChrome.AccentRed;

    private readonly InspectionController _controller;
    private readonly DeviceManagementController _deviceManagementController;
    private readonly string _account;
    private readonly Label _generatedAtLabel;
    private readonly Control _layoutRoot;
    private readonly InteractiveResizeFreezeController _interactiveResizeController;
    private readonly Button _selectFileButton;
    private readonly Button _clearButton;
    private readonly Button _importButton;
    private readonly Button _viewImportedButton;
    private readonly Button _viewPendingButton;
    private readonly DataGridView _previewGrid;
    private readonly PageChrome.ReadOnlyTextBlock _validationBlock;
    private readonly PageChrome.ReadOnlyTextBlock _nextStepBlock;

    private Label _previewSubtitleLabel = null!;
    private Label _validationSubtitleLabel = null!;
    private Label _statusSubtitleLabel = null!;
    private Label _nextStepSubtitleLabel = null!;
    private StatusSummaryRow _statusFileRow = null!;
    private StatusSummaryRow _statusValidRow = null!;
    private StatusSummaryRow _statusRiskRow = null!;
    private StatusSummaryRow _statusStateRow = null!;

    private CsvPreviewState? _currentPreview;
    private InspectionImportResultViewModel? _lastImportResult;
    private string? _currentFilePath;

    public DataInsightPageControl(
        InspectionController controller,
        DeviceManagementController deviceManagementController,
        string account)
    {
        _controller = controller;
        _deviceManagementController = deviceManagementController;
        _account = account;

        Dock = DockStyle.Fill;
        BackColor = PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);
        Padding = PageChrome.PagePadding;

        _generatedAtLabel = PageChrome.CreateInfoLabel();
        _selectFileButton = PageChrome.CreateActionButton("选择 CSV 文件", AccentBlue, true);
        _clearButton = PageChrome.CreateActionButton("清空", Color.FromArgb(128, 138, 154), false);
        _importButton = PageChrome.CreateActionButton("确认导入", AccentGreen, true);
        _viewImportedButton = PageChrome.CreateActionButton("查看本批记录", AccentBlue, false);
        _viewPendingButton = PageChrome.CreateActionButton("查看待闭环", AccentOrange, false);
        _previewGrid = CreatePreviewGrid();
        _validationBlock = PageChrome.CreateReadOnlyTextBlock();
        _nextStepBlock = PageChrome.CreateReadOnlyTextBlock();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = BuildHeader();
        PageChrome.BindControlHeightToRow(root, 0, header);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(BuildWorkspaceArea(), 0, 1);

        _layoutRoot = root;
        Controls.Add(root);
        _interactiveResizeController = new InteractiveResizeFreezeController(this, _layoutRoot, PageBackground);
        _layoutRoot.BringToFront();

        _selectFileButton.Click += (_, _) => ChooseCsvFile();
        _clearButton.Click += (_, _) => ResetState();
        _importButton.Click += (_, _) => ImportCurrentPreview();
        _viewImportedButton.Click += (_, _) => ViewImportedRequested?.Invoke(this, EventArgs.Empty);
        _viewPendingButton.Click += (_, _) => ViewPendingRequested?.Invoke(this, EventArgs.Empty);

        ApplyTheme();
        ResetState();
    }

    public event EventHandler? DataChanged;

    public event EventHandler? ViewImportedRequested;

    public event EventHandler? ViewPendingRequested;

    public string? LastImportedBatchKeyword => _lastImportResult?.BatchKeyword;

    public void ApplyTheme()
    {
        BackColor = PageBackground;
        PageChrome.ApplyGridTheme(_previewGrid);
        Invalidate(true);
    }

    public void BeginInteractiveResize()
    {
        _interactiveResizeController.Begin();
    }

    public void EndInteractiveResize()
    {
        if (!_interactiveResizeController.IsActive)
        {
            return;
        }

        _interactiveResizeController.End();
        _layoutRoot.PerformLayout();
        PerformLayout();
        Invalidate(true);
        Update();
    }

    private Control BuildHeader()
    {
        return PageChrome.CreatePageHeader(
            "数据导入",
            "导入前完成结构校验，导入后可在巡检记录中继续处理。",
            _generatedAtLabel,
            _selectFileButton,
            _importButton,
            _viewImportedButton,
            _viewPendingButton,
            _clearButton);
    }

    private Control BuildWorkspaceArea()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 54F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 46F));

        layout.Controls.Add(PageChrome.CreateSectionShell(
            "数据预览",
            "查看文件结构、行数和空值。",
            out _previewSubtitleLabel,
            _previewGrid,
            new Padding(0, 0, 12, 12)), 0, 0);
        layout.Controls.Add(BuildValidationPanel(), 1, 0);
        layout.Controls.Add(BuildStatusPanel(), 0, 1);
        layout.Controls.Add(BuildNextStepPanel(), 1, 1);
        return layout;
    }

    private Control BuildValidationPanel()
    {
        return PageChrome.CreateSectionShell(
            "校验结果",
            "查看阻断问题和导入提醒。",
            out _validationSubtitleLabel,
            CreateTextBlockShell(_validationBlock),
            new Padding(0, 0, 0, 12));
    }

    private Control BuildStatusPanel()
    {
        var statusLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

        _statusFileRow = CreateStatusRow("当前文件", AccentBlue, new Padding(0, 0, 0, 8));
        _statusValidRow = CreateStatusRow("有效记录", AccentGreen, new Padding(0, 0, 0, 8));
        _statusRiskRow = CreateStatusRow("风险记录", AccentOrange, new Padding(0, 0, 0, 8));
        _statusStateRow = CreateStatusRow("当前状态", AccentRed, Padding.Empty);

        var statusLayoutUsesCompactGrid = false;

        void ApplyStatusLayout()
        {
            var layoutHeight = statusLayout.ClientSize.Height;
            var useCompactGrid = statusLayout.ClientSize.Width >= 360 && layoutHeight > 0 && layoutHeight < 120;
            if (statusLayout.Controls.Count == 4 && statusLayoutUsesCompactGrid == useCompactGrid)
            {
                return;
            }

            statusLayoutUsesCompactGrid = useCompactGrid;
            statusLayout.SuspendLayout();
            statusLayout.Controls.Clear();
            statusLayout.ColumnStyles.Clear();
            statusLayout.RowStyles.Clear();

            if (useCompactGrid)
            {
                statusLayout.ColumnCount = 2;
                statusLayout.RowCount = 2;
                statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
                statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

                _statusFileRow.Margin = new Padding(0, 0, 8, 8);
                _statusValidRow.Margin = new Padding(0, 0, 0, 8);
                _statusRiskRow.Margin = new Padding(0, 0, 8, 0);
                _statusStateRow.Margin = Padding.Empty;

                statusLayout.Controls.Add(_statusFileRow, 0, 0);
                statusLayout.Controls.Add(_statusValidRow, 1, 0);
                statusLayout.Controls.Add(_statusRiskRow, 0, 1);
                statusLayout.Controls.Add(_statusStateRow, 1, 1);
            }
            else
            {
                statusLayout.ColumnCount = 1;
                statusLayout.RowCount = 4;
                statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
                statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
                statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
                statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

                _statusFileRow.Margin = new Padding(0, 0, 0, 8);
                _statusValidRow.Margin = new Padding(0, 0, 0, 8);
                _statusRiskRow.Margin = new Padding(0, 0, 0, 8);
                _statusStateRow.Margin = Padding.Empty;

                statusLayout.Controls.Add(_statusFileRow, 0, 0);
                statusLayout.Controls.Add(_statusValidRow, 0, 1);
                statusLayout.Controls.Add(_statusRiskRow, 0, 2);
                statusLayout.Controls.Add(_statusStateRow, 0, 3);
            }

            statusLayout.ResumeLayout(true);
        }

        statusLayout.SizeChanged += (_, _) => ApplyStatusLayout();

        statusLayout.Controls.Add(_statusFileRow, 0, 0);
        statusLayout.Controls.Add(_statusValidRow, 0, 1);
        statusLayout.Controls.Add(_statusRiskRow, 0, 2);
        statusLayout.Controls.Add(_statusStateRow, 0, 3);
        ApplyStatusLayout();

        return PageChrome.CreateSectionShell(
            "导入状态",
            "汇总当前文件、校验结果和导入状态。",
            out _statusSubtitleLabel,
            statusLayout,
            new Padding(0, 0, 12, 0));
    }

    private Control BuildNextStepPanel()
    {
        return PageChrome.CreateSectionShell(
            "下一步",
            "显示当前导入流程的下一步操作。",
            out _nextStepSubtitleLabel,
            CreateTextBlockShell(_nextStepBlock),
            Padding.Empty);
    }

    private static Control CreateTextBlockShell(PageChrome.ReadOnlyTextBlock block)
    {
        var shell = PageChrome.CreateSurfacePanel(
            new Padding(14),
            14,
            fillColor: InputBackground,
            borderColor: Color.FromArgb(70, SurfaceBorder));
        shell.Margin = Padding.Empty;
        block.Padding = new Padding(0);
        shell.Controls.Add(block);
        return shell;
    }

    private static StatusSummaryRow CreateStatusRow(string title, Color accent, Padding margin)
    {
        return new StatusSummaryRow(title, accent)
        {
            Margin = margin
        };
    }

    private sealed class StatusSummaryRow : Control
    {
        private readonly Font _titleFont = new("Microsoft YaHei UI", 8.8F);
        private readonly Font _valueFont = new("Microsoft YaHei UI", 12.5F, FontStyle.Bold);
        private readonly Font _noteFont = new("Microsoft YaHei UI", 8.6F);
        private readonly Color _accent;
        private string _valueText = "--";
        private string _noteText = string.Empty;

        public StatusSummaryRow(string title, Color accent)
        {
            Title = title;
            _accent = accent;
            Dock = DockStyle.Fill;
            Padding = new Padding(12, 5, 12, 5);
            BackColor = SurfaceBackground;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        public string Title { get; }

        public string ValueText
        {
            get => _valueText;
            set
            {
                var next = value ?? string.Empty;
                if (_valueText == next)
                {
                    return;
                }

                _valueText = next;
                Invalidate();
            }
        }

        public string NoteText
        {
            get => _noteText;
            set
            {
                var next = value ?? string.Empty;
                if (_noteText == next)
                {
                    return;
                }

                _noteText = next;
                Invalidate();
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var backgroundBrush = new SolidBrush(SurfaceBackground);
            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var outerRect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = PageChrome.CreateRoundedPath(outerRect, 12))
            using (var fillBrush = new SolidBrush(InputBackground))
            using (var borderPen = new Pen(Color.FromArgb(70, SurfaceBorder)))
            {
                e.Graphics.FillPath(fillBrush, path);
                e.Graphics.DrawPath(borderPen, path);
            }

            var contentBounds = new Rectangle(
                Padding.Left,
                Padding.Top,
                Math.Max(0, Width - Padding.Horizontal),
                Math.Max(0, Height - Padding.Vertical));
            if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
            {
                return;
            }

            const TextFormatFlags leftFlags =
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.PreserveGraphicsTranslateTransform;
            const TextFormatFlags rightFlags =
                TextFormatFlags.Right |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.PreserveGraphicsTranslateTransform;

            var markerRect = new Rectangle(contentBounds.X, contentBounds.Y + Math.Max(0, (contentBounds.Height - 4) / 2), 12, 4);
            using (var markerBrush = new SolidBrush(_accent))
            {
                e.Graphics.FillRectangle(markerBrush, markerRect);
            }

            var textLeft = markerRect.Right + 12;
            var textWidth = Math.Max(0, contentBounds.Right - textLeft);
            if (textWidth <= 0)
            {
                return;
            }

            var valueWidth = Math.Min(Math.Max(96, textWidth / 3), Math.Max(96, textWidth - 80));
            var titleWidth = Math.Max(0, textWidth - valueWidth - 12);
            var titleBounds = new Rectangle(textLeft, contentBounds.Y, titleWidth, contentBounds.Height);
            var valueBounds = new Rectangle(contentBounds.Right - valueWidth, contentBounds.Y, valueWidth, contentBounds.Height);

            var noteFits = !string.IsNullOrWhiteSpace(NoteText) && contentBounds.Height >= _titleFont.Height + _noteFont.Height + 6;
            if (noteFits)
            {
                var noteBounds = new Rectangle(textLeft, contentBounds.Y + _titleFont.Height + 2, textWidth, Math.Max(0, contentBounds.Height - _titleFont.Height - 2));
                titleBounds = new Rectangle(textLeft, contentBounds.Y, titleWidth, _titleFont.Height + 2);
                valueBounds = new Rectangle(contentBounds.Right - valueWidth, contentBounds.Y, valueWidth, _titleFont.Height + 2);
                TextRenderer.DrawText(e.Graphics, NoteText, _noteFont, noteBounds, TextMutedColor, leftFlags);
            }

            TextRenderer.DrawText(e.Graphics, Title, _titleFont, titleBounds, TextMutedColor, leftFlags);
            TextRenderer.DrawText(e.Graphics, ValueText, _valueFont, valueBounds, TextPrimaryColor, rightFlags);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _valueFont.Dispose();
                _noteFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private void ChooseCsvFile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            Title = "选择要导入的 CSV 文件",
            Multiselect = false,
            RestoreDirectory = true
        };

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        try
        {
            LoadCsvFile(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), ex.Message, "加载失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void LoadCsvFile(string filePath)
    {
        _currentFilePath = filePath;
        _currentPreview = ParseCsv(filePath);
        _lastImportResult = null;
        _previewGrid.DataSource = _currentPreview.PreviewTable;
        _generatedAtLabel.Text = $"最近加载：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        RefreshDisplayState();
    }

    private void ImportCurrentPreview()
    {
        if (_currentPreview is null || !_currentPreview.CanImport || _currentFilePath is null)
        {
            return;
        }

        try
        {
            _lastImportResult = _controller.Import(_currentPreview.Entries, _currentFilePath);
            _deviceManagementController.EnsureDevicesFromInspection(_currentPreview.Entries);
            _generatedAtLabel.Text = $"最近导入：{_lastImportResult.ImportedAt:yyyy-MM-dd HH:mm:ss}";
            DataChanged?.Invoke(this, EventArgs.Empty);
            RefreshDisplayState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), ex.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ResetState()
    {
        _currentPreview = null;
        _lastImportResult = null;
        _currentFilePath = null;
        _previewGrid.DataSource = null;
        _previewGrid.Columns.Clear();
        _generatedAtLabel.Text = "尚未选择导入文件";
        RefreshDisplayState();
    }

    private void RefreshDisplayState()
    {
        UpdatePreviewSubtitle();
        UpdateStatusSummary();
        UpdateValidationSummary();
        UpdateNextStepSummary();
        UpdateActionState();
    }

    private void UpdatePreviewSubtitle()
    {
        if (_currentPreview is null)
        {
            _previewSubtitleLabel.Text = "请选择 CSV 文件以预览数据。";
            return;
        }

        if (_currentPreview.RowCount == 0)
        {
            _previewSubtitleLabel.Text = "文件仅包含表头，暂无数据行。";
            return;
        }

        _previewSubtitleLabel.Text = _lastImportResult is not null
            ? $"已导入，预览显示前 {_currentPreview.DisplayedRowCount} 行。"
            : $"显示前 {_currentPreview.DisplayedRowCount} 行，空值 {_currentPreview.MissingValueCount} 个。";
    }

    private void UpdateStatusSummary()
    {
        if (_lastImportResult is not null)
        {
            _statusSubtitleLabel.Text = _lastImportResult.PendingCount > 0
                ? $"导入完成，本批次还有 {_lastImportResult.PendingCount} 条待闭环。"
                : "导入完成，这批数据没有新增待闭环。";

            _statusFileRow.ValueText = _lastImportResult.SourceFileName;
            _statusFileRow.NoteText = $"批次 {_lastImportResult.BatchKeyword}";

            _statusValidRow.ValueText = _lastImportResult.ImportedCount.ToString();
            _statusValidRow.NoteText = $"模板新增 {_lastImportResult.TemplateCreatedCount} / 更新 {_lastImportResult.TemplateUpdatedCount}";

            var riskCount = _lastImportResult.WarningCount + _lastImportResult.AbnormalCount;
            _statusRiskRow.ValueText = riskCount.ToString();
            _statusRiskRow.NoteText = $"预警 {_lastImportResult.WarningCount} / 异常 {_lastImportResult.AbnormalCount}";

            _statusStateRow.ValueText = "已导入";
            _statusStateRow.NoteText = _lastImportResult.PendingCount > 0
                ? "查看待闭环记录。"
                : "查看本批记录。";
            return;
        }

        if (_currentPreview is not null)
        {
            _statusSubtitleLabel.Text = _currentPreview.CanImport
                ? "校验通过，可导入。"
                : $"当前文件还有 {_currentPreview.ValidationErrors.Count} 个阻断问题。";

            _statusFileRow.ValueText = _currentPreview.FileName;
            _statusFileRow.NoteText = $"原始 {_currentPreview.RowCount} 行 / {_currentPreview.ColumnCount} 列";

            _statusValidRow.ValueText = _currentPreview.ValidEntryCount.ToString();
            _statusValidRow.NoteText = _currentPreview.CanImport
                ? "模板校验通过，可写入系统。"
                : "有错误的行不会进入导入列表。";

            var riskCount = _currentPreview.WarningCount + _currentPreview.AbnormalCount;
            _statusRiskRow.ValueText = riskCount.ToString();
            _statusRiskRow.NoteText = $"正常 {_currentPreview.NormalCount} / 预警 {_currentPreview.WarningCount} / 异常 {_currentPreview.AbnormalCount}";

            _statusStateRow.ValueText = _currentPreview.CanImport ? "待导入" : "不可导入";
            _statusStateRow.NoteText = _currentPreview.CanImport
                ? "选择“确认导入”。"
                : "请修正阻断问题。";
            return;
        }

        _statusSubtitleLabel.Text = "尚未选择文件。";
        _statusFileRow.ValueText = "--";
        _statusFileRow.NoteText = "请选择 CSV 文件。";
        _statusValidRow.ValueText = "0";
        _statusValidRow.NoteText = "有效记录将在校验后显示。";
        _statusRiskRow.ValueText = "0";
        _statusRiskRow.NoteText = "预警和异常将在校验后显示。";
        _statusStateRow.ValueText = "未开始";
        _statusStateRow.NoteText = "请选择文件并完成校验。";
    }

    private void UpdateValidationSummary()
    {
        if (_currentPreview is null)
        {
            _validationSubtitleLabel.Text = "选择文件后显示校验结果。";
            _validationBlock.Text =
                "CSV 模板要求：\r\n\r\n" +
                "1. 必填列：产线、设备名称、点检项目、状态、点检时间。\r\n" +
                "2. 可选列：点检人、测量值、备注。\r\n" +
                "3. 状态只支持：正常 / 预警 / 异常。";
            return;
        }

        if (_lastImportResult is not null)
        {
            _validationSubtitleLabel.Text = _lastImportResult.PendingCount > 0
                ? $"导入完成，本批次仍有 {_lastImportResult.PendingCount} 条待闭环。"
                : "导入完成，这批数据没有新增待闭环。";
            _validationBlock.Text =
                $"批次：{_lastImportResult.BatchKeyword}\r\n" +
                $"来源文件：{_lastImportResult.SourceFileName}\r\n" +
                $"导入记录：{_lastImportResult.ImportedCount} 条\r\n" +
                $"状态分布：正常 {_lastImportResult.NormalCount} / 预警 {_lastImportResult.WarningCount} / 异常 {_lastImportResult.AbnormalCount}\r\n" +
                (_lastImportResult.PendingCount > 0
                    ? "校验已通过，重点关注待闭环记录。"
                    : "校验已通过，可在巡检页查看本批记录。");
            return;
        }

        var lines = new List<string>();
        if (_currentPreview.ValidationErrors.Count > 0)
        {
            _validationSubtitleLabel.Text = $"存在 {_currentPreview.ValidationErrors.Count} 个阻断问题。";
            lines.Add("阻断问题：");
            foreach (var error in _currentPreview.ValidationErrors)
            {
                lines.Add($"- {error}");
            }
        }
        else
        {
            _validationSubtitleLabel.Text = _currentPreview.Warnings.Count > 0
                ? $"无阻断问题，另有 {_currentPreview.Warnings.Count} 条提醒。"
                : "校验通过，可导入。";
            lines.Add("阻断问题：无");
        }

        lines.Add(string.Empty);
        lines.Add($"风险记录：{_currentPreview.WarningCount + _currentPreview.AbnormalCount} 条");
        lines.Add($"状态分布：正常 {_currentPreview.NormalCount} / 预警 {_currentPreview.WarningCount} / 异常 {_currentPreview.AbnormalCount}");

        if (_currentPreview.Warnings.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("提醒：");
            foreach (var warning in _currentPreview.Warnings.Take(5))
            {
                lines.Add($"- {warning}");
            }
        }

        _validationBlock.Text = string.Join(Environment.NewLine, lines);
    }

    private void UpdateNextStepSummary()
    {
        if (_lastImportResult is not null && _currentPreview is not null)
        {
            _nextStepSubtitleLabel.Text = _lastImportResult.PendingCount > 0
                ? "导入完成，请处理待闭环记录。"
                : "导入完成，可在巡检页查看本批记录。";
            _nextStepBlock.Text = BuildImportAnalysis(_lastImportResult, _currentPreview);
            return;
        }

        if (_currentPreview is null)
        {
            _nextStepSubtitleLabel.Text = "请选择文件并完成校验。";
            _nextStepBlock.Text =
                "导入流程：\r\n\r\n" +
                "1. 选择 CSV 文件。\r\n" +
                "2. 查看预览和校验结果。\r\n" +
                "3. 校验通过后确认导入。";
            return;
        }

        _nextStepSubtitleLabel.Text = _currentPreview.CanImport
            ? "校验通过，可确认导入。"
            : "当前文件不可导入。";
        _nextStepBlock.Text = BuildPreviewAnalysis(_currentPreview);
    }

    private void UpdateActionState()
    {
        _importButton.Enabled = _currentPreview?.CanImport == true;
        _importButton.Visible = _lastImportResult is null;
        _viewImportedButton.Enabled = _lastImportResult is not null;
        _viewImportedButton.Visible = _lastImportResult is not null;
        _viewPendingButton.Enabled = (_lastImportResult?.PendingCount ?? 0) > 0;
        _viewPendingButton.Visible = (_lastImportResult?.PendingCount ?? 0) > 0;
        _clearButton.Visible = _currentPreview is not null || _lastImportResult is not null;
    }

    private string BuildPreviewAnalysis(CsvPreviewState preview)
    {
        var lines = new List<string>();
        if (preview.CanImport)
        {
            lines.Add("当前文件可以导入。请选择“确认导入”。");
        }
        else
        {
            lines.Add("当前文件不可导入。请按校验结果修正 CSV。");
        }

        lines.Add($"有效记录 {preview.ValidEntryCount} 条，预警 {preview.WarningCount} 条，异常 {preview.AbnormalCount} 条。");
        lines.Add($"原始数据 {preview.RowCount} 行 / {preview.ColumnCount} 列，空值 {preview.MissingValueCount} 个。");

        if (preview.ValidationErrors.Count > 0)
        {
            lines.Add("需修正以下问题：");
            foreach (var error in preview.ValidationErrors.Take(5))
            {
                lines.Add($"- {error}");
            }
        }

        if (preview.Warnings.Count > 0)
        {
            lines.Add("导入提醒：");
            foreach (var warning in preview.Warnings.Take(3))
            {
                lines.Add($"- {warning}");
            }
        }

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static string BuildImportAnalysis(InspectionImportResultViewModel result, CsvPreviewState preview)
    {
        var lines = new List<string>
        {
            $"导入完成：{result.ImportedCount} 条记录已写入系统。",
            $"本次批次：{result.BatchKeyword}。可在巡检页处理本批记录。",
            $"状态分布：正常 {result.NormalCount} / 预警 {result.WarningCount} / 异常 {result.AbnormalCount}。",
            $"模板同步：新增 {result.TemplateCreatedCount} 个，更新 {result.TemplateUpdatedCount} 个。",
            $"来源文件：{result.SourceFileName}。"
        };

        if (result.PendingCount > 0)
        {
            lines.Add("本批数据存在待闭环项，请查看待闭环记录。");
        }
        else if (preview.ValidEntryCount > 0)
        {
            lines.Add("本批数据未新增待闭环项，可在巡检页查询或导出。");
        }

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private CsvPreviewState ParseCsv(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("没有找到要导入的 CSV 文件。", filePath);
        }

        using var parser = new TextFieldParser(filePath, Encoding.UTF8, detectEncoding: true)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            throw new InvalidOperationException("CSV 文件是空的。");
        }

        var rawHeaders = parser.ReadFields();
        if (rawHeaders is null || rawHeaders.Length == 0)
        {
            throw new InvalidOperationException("CSV 表头读取失败。");
        }

        var warnings = new List<string>();
        var headers = BuildHeaders(rawHeaders, warnings);
        var table = new DataTable();
        foreach (var header in headers)
        {
            table.Columns.Add(header);
        }

        var columnMap = ResolveColumns(headers);
        var errors = ValidateRequiredColumns(columnMap);
        var entries = new List<InspectionEntryViewModel>();
        var missingValueCount = 0;
        var rowCount = 0;
        var displayedRowCount = 0;
        var statusCounts = new Dictionary<InspectionStatus, int>
        {
            [InspectionStatus.Normal] = 0,
            [InspectionStatus.Warning] = 0,
            [InspectionStatus.Abnormal] = 0
        };

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? [];
            if (fields.All(field => string.IsNullOrWhiteSpace(field)))
            {
                continue;
            }

            EnsureColumnCount(fields.Length, table);
            var values = BuildRowValues(table.Columns.Count, fields, ref missingValueCount);

            if (displayedRowCount < PreviewLimit)
            {
                table.Rows.Add(values.Cast<object>().ToArray());
                displayedRowCount++;
            }

            rowCount++;
            TryCreateEntry(values, rowCount + 1, columnMap, errors, entries, statusCounts);
        }

        return new CsvPreviewState(
            Path.GetFileName(filePath),
            table,
            entries,
            errors,
            warnings,
            rowCount,
            table.Columns.Count,
            missingValueCount,
            displayedRowCount,
            statusCounts[InspectionStatus.Normal],
            statusCounts[InspectionStatus.Warning],
            statusCounts[InspectionStatus.Abnormal]);
    }

    private string[] BuildRowValues(int columnCount, IReadOnlyList<string> fields, ref int missingValueCount)
    {
        var values = new string[columnCount];
        for (var index = 0; index < columnCount; index++)
        {
            var value = index < fields.Count ? fields[index].Trim() : string.Empty;
            values[index] = value;
            if (string.IsNullOrWhiteSpace(value))
            {
                missingValueCount++;
            }
        }

        return values;
    }

    private void TryCreateEntry(
        IReadOnlyList<string> values,
        int displayRowNumber,
        IReadOnlyDictionary<ImportColumn, int> columnMap,
        ICollection<string> errors,
        ICollection<InspectionEntryViewModel> entries,
        IDictionary<InspectionStatus, int> statusCounts)
    {
        if (errors.Any(error => error.StartsWith("缺少", StringComparison.Ordinal)))
        {
            return;
        }

        var lineName = GetRequiredValue(values, columnMap, ImportColumn.LineName);
        var deviceName = GetRequiredValue(values, columnMap, ImportColumn.DeviceName);
        var inspectionItem = GetRequiredValue(values, columnMap, ImportColumn.InspectionItem);
        var statusText = GetRequiredValue(values, columnMap, ImportColumn.Status);
        var checkedAtText = GetRequiredValue(values, columnMap, ImportColumn.CheckedAt);

        if (string.IsNullOrWhiteSpace(lineName) ||
            string.IsNullOrWhiteSpace(deviceName) ||
            string.IsNullOrWhiteSpace(inspectionItem) ||
            string.IsNullOrWhiteSpace(statusText) ||
            string.IsNullOrWhiteSpace(checkedAtText))
        {
            AddError(errors, $"第 {displayRowNumber} 行有必填列为空。");
            return;
        }

        if (!TryParseStatus(statusText, out var status))
        {
            AddError(errors, $"第 {displayRowNumber} 行状态“{statusText}”无法识别。");
            return;
        }

        if (!DateTime.TryParse(checkedAtText, out var checkedAt))
        {
            AddError(errors, $"第 {displayRowNumber} 行点检时间“{checkedAtText}”无法识别。");
            return;
        }

        var measuredValue = 0m;
        var measuredText = GetOptionalValue(values, columnMap, ImportColumn.MeasuredValue);
        if (!string.IsNullOrWhiteSpace(measuredText) && !decimal.TryParse(measuredText, out measuredValue))
        {
            AddError(errors, $"第 {displayRowNumber} 行测量值“{measuredText}”不是有效数字。");
            return;
        }

        var inspector = GetOptionalValue(values, columnMap, ImportColumn.Inspector);
        entries.Add(new InspectionEntryViewModel
        {
            LineName = lineName,
            DeviceName = deviceName,
            InspectionItem = inspectionItem,
            Inspector = string.IsNullOrWhiteSpace(inspector) ? _account : inspector,
            Status = status,
            MeasuredValue = measuredValue,
            CheckedAt = checkedAt,
            Remark = GetOptionalValue(values, columnMap, ImportColumn.Remark)
        });
        statusCounts[status]++;
    }

    private static IReadOnlyDictionary<ImportColumn, int> ResolveColumns(IReadOnlyList<string> headers)
    {
        return new Dictionary<ImportColumn, int>
        {
            [ImportColumn.LineName] = FindColumn(headers, "产线", "line", "line_name"),
            [ImportColumn.DeviceName] = FindColumn(headers, "设备", "设备名称", "device", "device_name"),
            [ImportColumn.InspectionItem] = FindColumn(headers, "点检项目", "巡检项目", "inspection_item", "item"),
            [ImportColumn.Status] = FindColumn(headers, "状态", "结果", "status", "result"),
            [ImportColumn.CheckedAt] = FindColumn(headers, "点检时间", "巡检时间", "时间", "checked_at", "checkedat", "time"),
            [ImportColumn.Inspector] = FindColumn(headers, "点检人", "巡检人", "inspector"),
            [ImportColumn.MeasuredValue] = FindColumn(headers, "测量值", "数值", "measured_value", "value"),
            [ImportColumn.Remark] = FindColumn(headers, "备注", "remark", "comment")
        };
    }

    private static List<string> ValidateRequiredColumns(IReadOnlyDictionary<ImportColumn, int> columnMap)
    {
        var errors = new List<string>();
        foreach (var column in new[]
                 {
                     ImportColumn.LineName,
                     ImportColumn.DeviceName,
                     ImportColumn.InspectionItem,
                     ImportColumn.Status,
                     ImportColumn.CheckedAt
                 })
        {
            if (columnMap[column] < 0)
            {
                errors.Add($"缺少必填列：{GetColumnDisplayName(column)}。");
            }
        }

        return errors;
    }

    private static List<string> BuildHeaders(IReadOnlyList<string> rawHeaders, ICollection<string> warnings)
    {
        var headers = new List<string>(rawHeaders.Count);
        var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < rawHeaders.Count; index++)
        {
            var header = string.IsNullOrWhiteSpace(rawHeaders[index])
                ? $"列{index + 1}"
                : rawHeaders[index].Trim();

            if (string.IsNullOrWhiteSpace(rawHeaders[index]))
            {
                warnings.Add($"第 {index + 1} 列表头为空，系统已自动补名。");
            }

            if (nameCounts.TryGetValue(header, out var count))
            {
                count++;
                nameCounts[header] = count;
                warnings.Add($"表头“{header}”重复，系统已自动追加序号。");
                header = $"{header}_{count}";
            }
            else
            {
                nameCounts[header] = 1;
            }

            headers.Add(header);
        }

        return headers;
    }

    private static void EnsureColumnCount(int fieldCount, DataTable table)
    {
        for (var index = table.Columns.Count; index < fieldCount; index++)
        {
            table.Columns.Add($"扩展列{index + 1}");
        }
    }

    private static int FindColumn(IReadOnlyList<string> headers, params string[] aliases)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index];
            if (aliases.Any(alias => header.Contains(alias, StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return -1;
    }

    private static string GetRequiredValue(IReadOnlyList<string> values, IReadOnlyDictionary<ImportColumn, int> columnMap, ImportColumn column)
    {
        var index = columnMap[column];
        return index >= 0 && index < values.Count ? values[index].Trim() : string.Empty;
    }

    private static string GetOptionalValue(IReadOnlyList<string> values, IReadOnlyDictionary<ImportColumn, int> columnMap, ImportColumn column)
    {
        var index = columnMap[column];
        return index >= 0 && index < values.Count ? values[index].Trim() : string.Empty;
    }

    private static bool TryParseStatus(string value, out InspectionStatus status)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "正常":
            case "normal":
                status = InspectionStatus.Normal;
                return true;
            case "预警":
            case "warning":
                status = InspectionStatus.Warning;
                return true;
            case "异常":
            case "abnormal":
                status = InspectionStatus.Abnormal;
                return true;
            default:
                status = InspectionStatus.Normal;
                return false;
        }
    }

    private static void AddError(ICollection<string> errors, string message)
    {
        if (errors.Count < 12)
        {
            errors.Add(message);
        }
    }

    private static string GetColumnDisplayName(ImportColumn column)
    {
        return column switch
        {
            ImportColumn.LineName => "产线",
            ImportColumn.DeviceName => "设备名称",
            ImportColumn.InspectionItem => "点检项目",
            ImportColumn.Status => "状态",
            ImportColumn.CheckedAt => "点检时间",
            ImportColumn.Inspector => "点检人",
            ImportColumn.MeasuredValue => "测量值",
            ImportColumn.Remark => "备注",
            _ => "未知列"
        };
    }

    private static DataGridView CreatePreviewGrid()
    {
        var grid = new BufferedDataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SurfaceBackground,
            BorderStyle = BorderStyle.None,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            Dock = DockStyle.Fill,
            EnableHeadersVisualStyles = false,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        PageChrome.ApplyGridTheme(grid);
        return grid;
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

    private enum ImportColumn
    {
        LineName,
        DeviceName,
        InspectionItem,
        Inspector,
        Status,
        MeasuredValue,
        CheckedAt,
        Remark
    }

    private sealed record CsvPreviewState(
        string FileName,
        DataTable PreviewTable,
        IReadOnlyList<InspectionEntryViewModel> Entries,
        IReadOnlyList<string> ValidationErrors,
        IReadOnlyList<string> Warnings,
        int RowCount,
        int ColumnCount,
        int MissingValueCount,
        int DisplayedRowCount,
        int NormalCount,
        int WarningCount,
        int AbnormalCount)
    {
        public bool CanImport => ValidationErrors.Count == 0 && Entries.Count > 0;

        public int ValidEntryCount => Entries.Count;
    }
}
