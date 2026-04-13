using App.Core.Models;
using WinFormsApp.Controllers;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Views;

internal sealed class DeviceMonitorPageControl : UserControl, IInteractiveResizeAware
{
    private static readonly Color PageBackground = PageChrome.PageBackground;
    private static readonly Color SurfaceBackground = PageChrome.SurfaceBackground;
    private static readonly Color SurfaceBorder = PageChrome.SurfaceBorder;
    private static readonly Color TextPrimaryColor = PageChrome.TextPrimary;
    private static readonly Color TextSecondaryColor = PageChrome.TextSecondary;
    private static readonly Color TextMutedColor = PageChrome.TextMuted;
    private static readonly Color AccentBlue = PageChrome.AccentBlue;
    private static readonly Color SuccessColor = PageChrome.AccentGreen;
    private static readonly Color WarningColor = PageChrome.AccentOrange;
    private static readonly Color DangerColor = PageChrome.AccentRed;

    private readonly InspectionController _inspectionController;
    private readonly DeviceManagementController _deviceManagementController;
    private readonly Label _generatedAtLabel;
    private readonly Control _layoutRoot;
    private readonly InteractiveResizeFreezeController _interactiveResizeController;
    private Label _deviceCountValueLabel = null!;
    private Label _issueDeviceValueLabel = null!;
    private Label _issueDeviceNoteLabel = null!;
    private Label _pendingCountValueLabel = null!;
    private Label _pendingCountNoteLabel = null!;
    private Label _healthyCountValueLabel = null!;
    private Label _healthyCountNoteLabel = null!;
    private Label _focusDeviceLabel = null!;
    private Label _focusDetailLabel = null!;
    private DataGridView _deviceGrid = null!;
    private DataGridView _attentionGrid = null!;
    private Label _attentionEmptyLabel = null!;

    private IReadOnlyList<DeviceRow> _deviceRows = Array.Empty<DeviceRow>();
    private IReadOnlyList<AttentionRow> _attentionRows = Array.Empty<AttentionRow>();

    public DeviceMonitorPageControl(
        InspectionController inspectionController,
        DeviceManagementController deviceManagementController)
    {
        _inspectionController = inspectionController;
        _deviceManagementController = deviceManagementController;
        Dock = DockStyle.Fill;
        BackColor = PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);
        Padding = PageChrome.PagePadding;

        _generatedAtLabel = PageChrome.CreateInfoLabel();
        var refreshButton = CreateRefreshButton();
        refreshButton.Click += (_, _) => RefreshData();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = BuildHeader(refreshButton);
        PageChrome.BindControlHeightToRow(root, 0, header);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(BuildSummaryArea(), 0, 1);
        root.Controls.Add(BuildBodyArea(), 0, 2);

        _layoutRoot = root;
        Controls.Add(root);
        _interactiveResizeController = new InteractiveResizeFreezeController(this, _layoutRoot, PageBackground);
        _layoutRoot.BringToFront();
        ApplyTheme();
        Load += (_, _) => RefreshData();
    }

    public void RefreshData()
    {
        var inspectionDashboard = _inspectionController.Load(new InspectionFilterViewModel());
        var deviceDashboard = _deviceManagementController.Load(new DeviceFilterViewModel());
        var records = inspectionDashboard.Records
            .Where(record => !record.IsRevoked)
            .OrderByDescending(record => record.CheckedAtValue)
            .ToList();

        var recordsByDevice = records
            .GroupBy(record => BuildDeviceKey(record.LineName, record.DeviceName))
            .ToDictionary(group => group.Key, group => group.ToList());
        var ledgerKeys = deviceDashboard.Devices
            .Select(device => BuildDeviceKey(device.LineName, device.DeviceName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ledgerRows = deviceDashboard.Devices
            .Select(device =>
            {
                recordsByDevice.TryGetValue(BuildDeviceKey(device.LineName, device.DeviceName), out var deviceRecords);
                return BuildDeviceRow(device, deviceRecords ?? []);
            });
        var inspectionOnlyRows = recordsByDevice
            .Where(pair => !ledgerKeys.Contains(pair.Key))
            .Select(pair => BuildInspectionOnlyRow(pair.Value));

        _deviceRows = ledgerRows
            .Concat(inspectionOnlyRows)
            .OrderBy(row => row.AttentionRank)
            .ThenByDescending(row => row.PendingCount)
            .ThenBy(row => row.LineName)
            .ThenBy(row => row.DeviceName)
            .ToList();

        var inspectionAttentionRows = records
            .Where(record => record.Status != InspectionStatus.Normal && !record.IsClosed)
            .Select(record => new AttentionRow
            {
                DeviceName = record.DeviceName,
                InspectionItem = record.InspectionItem,
                StatusText = record.StatusText,
                CheckedAt = record.CheckedAtValue.ToString("MM-dd HH:mm"),
                Detail = $"{record.LineName} / {record.Inspector}"
            });
        var deviceAttentionRows = deviceDashboard.Devices
            .Where(device => device.Status != ManagedDeviceStatus.Active || string.IsNullOrWhiteSpace(device.CommunicationAddress))
            .Select(BuildDeviceAttentionRow);

        _attentionRows = deviceAttentionRows
            .Concat(inspectionAttentionRows)
            .Take(8)
            .ToList();

        var focusRow = _deviceRows.FirstOrDefault(row => row.PendingCount > 0) ?? _deviceRows.FirstOrDefault();
        var pendingDeviceCount = _deviceRows.Count(row => row.PendingCount > 0);
        var healthyDeviceCount = Math.Max(0, _deviceRows.Count - pendingDeviceCount);

        _deviceCountValueLabel.Text = _deviceRows.Count.ToString();
        _pendingCountValueLabel.Text = pendingDeviceCount.ToString();
        _pendingCountNoteLabel.Text = pendingDeviceCount == 0 ? "当前没有待处理设备" : "优先处理有未闭环问题的设备";
        _healthyCountValueLabel.Text = healthyDeviceCount.ToString();
        _healthyCountNoteLabel.Text = healthyDeviceCount == 0 ? "暂时没有稳定设备" : "最近一次巡检结果正常";

        if (focusRow is null)
        {
            _issueDeviceValueLabel.Text = "--";
            _issueDeviceNoteLabel.Text = "暂无设备数据";
            _focusDeviceLabel.Text = "暂无重点设备";
            _focusDetailLabel.Text = "暂无巡检记录。";
        }
        else
        {
            _issueDeviceValueLabel.Text = focusRow.DeviceName;
            _issueDeviceNoteLabel.Text = $"{focusRow.LineName} / {focusRow.AttentionLevel}";
            _focusDeviceLabel.Text = $"{focusRow.LineName} / {focusRow.DeviceName}";
            _focusDetailLabel.Text = focusRow.PendingCount > 0
                ? $"当前有 {focusRow.PendingCount} 条待处理问题，请优先处理。"
                : $"最近巡检时间 {focusRow.LatestCheckedAt}，当前状态稳定。";
        }

        _generatedAtLabel.Text = $"最近刷新 {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        _deviceGrid.DataSource = _deviceRows.ToList();
        _attentionGrid.DataSource = _attentionRows.ToList();
        _attentionGrid.Visible = _attentionRows.Count > 0;
        _attentionEmptyLabel.Visible = _attentionRows.Count == 0;
    }

    public void ApplyTheme()
    {
        BackColor = PageBackground;
        PageChrome.ApplyGridTheme(_deviceGrid);
        PageChrome.ApplyGridTheme(_attentionGrid);
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

    private Control BuildHeader(Button refreshButton)
    {
        return PageChrome.CreatePageHeader(
            "设备监控",
            "按设备维度汇总巡检状态和待处理问题。",
            _generatedAtLabel,
            refreshButton);
    }

    private Control BuildSummaryArea()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (var index = 0; index < 4; index++)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        _deviceCountValueLabel = PageChrome.CreateValueLabel();
        var deviceNoteLabel = PageChrome.CreateNoteLabel("来自设备台账，巡检记录作补充");

        _issueDeviceValueLabel = PageChrome.CreateValueLabel(16F);
        _issueDeviceNoteLabel = PageChrome.CreateNoteLabel();

        _pendingCountValueLabel = PageChrome.CreateValueLabel();
        _pendingCountNoteLabel = PageChrome.CreateNoteLabel();

        _healthyCountValueLabel = PageChrome.CreateValueLabel();
        _healthyCountNoteLabel = PageChrome.CreateNoteLabel();

        layout.Controls.Add(PageChrome.CreateMetricCard("设备总数", AccentBlue, _deviceCountValueLabel, deviceNoteLabel), 0, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("当前关注设备", WarningColor, _issueDeviceValueLabel, _issueDeviceNoteLabel), 1, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("待处理设备", DangerColor, _pendingCountValueLabel, _pendingCountNoteLabel), 2, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("状态稳定", SuccessColor, _healthyCountValueLabel, _healthyCountNoteLabel, Padding.Empty), 3, 0);
        return layout;
    }

    private Control BuildBodyArea()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _deviceGrid = CreateGrid();
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.LineName), "产线", 90));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.DeviceName), "设备", 160));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.DeviceStatus), "台账状态", 90));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.CommunicationState), "通信", 80));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.LatestStatus), "巡检状态", 90));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.AttentionLevel), "关注级别", 120));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.PendingCount), "待处理", 70));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.LatestCheckedAt), "最近巡检", 150));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.Owner), "负责人", 90));
        _deviceGrid.CellFormatting += DeviceGridOnCellFormatting;

        var devicePanel = PageChrome.CreateSectionShell(
            "设备列表",
            "从设备台账出发，叠加巡检、通信和维护状态。",
            out _,
            _deviceGrid,
            new Padding(0, 0, 12, 0));

        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 188F));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _focusDeviceLabel = PageChrome.CreateValueLabel(16F);
        _focusDeviceLabel.Dock = DockStyle.Top;
        _focusDeviceLabel.Margin = Padding.Empty;
        _focusDetailLabel = PageChrome.CreateNoteLabel();
        _focusDetailLabel.AutoSize = false;
        _focusDetailLabel.Dock = DockStyle.Fill;
        _focusDetailLabel.TextAlign = ContentAlignment.TopLeft;

        var focusContent = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        focusContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        focusContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        focusContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        focusContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        focusContent.Controls.Add(PageChrome.CreateNoteLabel("当前优先关注设备", 8.8F, TextMutedColor), 0, 0);
        focusContent.Controls.Add(_focusDeviceLabel, 0, 1);
        focusContent.Controls.Add(_focusDetailLabel, 0, 2);

        var focusPanel = PageChrome.CreateSectionShell(
            "重点设备",
            "展示当前最需要关注的设备。",
            out _,
            focusContent,
            new Padding(0, 0, 0, 12));

        _attentionGrid = CreateGrid();
        _attentionGrid.Columns.Add(CreateTextColumn(nameof(AttentionRow.DeviceName), "设备", 120));
        _attentionGrid.Columns.Add(CreateTextColumn(nameof(AttentionRow.InspectionItem), "问题项", 150));
        _attentionGrid.Columns.Add(CreateTextColumn(nameof(AttentionRow.StatusText), "状态", 70));
        _attentionGrid.Columns.Add(CreateTextColumn(nameof(AttentionRow.CheckedAt), "时间", 100));
        _attentionGrid.Columns.Add(CreateTextColumn(nameof(AttentionRow.Detail), "补充", 150));
        _attentionGrid.CellFormatting += AttentionGridOnCellFormatting;

        _attentionEmptyLabel = PageChrome.CreateEmptyStateLabel("当前没有待关注记录");
        var attentionBody = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        attentionBody.Controls.Add(_attentionGrid);
        attentionBody.Controls.Add(_attentionEmptyLabel);

        var attentionPanel = PageChrome.CreateSectionShell(
            "最近关注",
            "展示最近需要关注的记录。",
            out _,
            attentionBody,
            Padding.Empty);

        rightLayout.Controls.Add(focusPanel, 0, 0);
        rightLayout.Controls.Add(attentionPanel, 0, 1);

        layout.Controls.Add(devicePanel, 0, 0);
        layout.Controls.Add(rightLayout, 1, 0);
        return layout;
    }

    private static DeviceRow BuildDeviceRow(DeviceRowViewModel device, IReadOnlyList<InspectionRecordViewModel> records)
    {
        var latest = records.FirstOrDefault();
        var pendingCount = records.Count(record => record.Status != InspectionStatus.Normal && !record.IsClosed);
        var abnormalCount = records.Count(record => record.Status == InspectionStatus.Abnormal && !record.IsClosed);
        var warningCount = records.Count(record => record.Status == InspectionStatus.Warning && !record.IsClosed);
        var isCommunicationConfigured = !string.IsNullOrWhiteSpace(device.CommunicationAddress);
        var attentionRank = GetAttentionRank(device.Status, isCommunicationConfigured, abnormalCount, warningCount);

        return new DeviceRow
        {
            LineName = device.LineName,
            DeviceName = device.DeviceName,
            DeviceStatus = device.StatusText,
            CommunicationState = isCommunicationConfigured ? "已配置" : "未配置",
            LatestStatus = latest?.StatusText ?? "暂无巡检",
            LatestCheckedAt = latest?.CheckedAtValue.ToString("MM-dd HH:mm") ?? "--",
            Owner = string.IsNullOrWhiteSpace(device.Owner) ? "--" : device.Owner,
            PendingCount = pendingCount + (attentionRank is >= 2 and <= 4 ? 1 : 0),
            AttentionRank = attentionRank,
            AttentionLevel = attentionRank switch
            {
                0 => "异常待处理",
                1 => "预警待确认",
                2 => "设备已停用",
                3 => "维护中",
                4 => "通信未配置",
                _ => "状态稳定"
            }
        };
    }

    private static DeviceRow BuildInspectionOnlyRow(IReadOnlyList<InspectionRecordViewModel> records)
    {
        var latest = records.First();
        var pendingCount = records.Count(record => record.Status != InspectionStatus.Normal && !record.IsClosed);
        var abnormalCount = records.Count(record => record.Status == InspectionStatus.Abnormal && !record.IsClosed);
        var warningCount = records.Count(record => record.Status == InspectionStatus.Warning && !record.IsClosed);

        return new DeviceRow
        {
            LineName = latest.LineName,
            DeviceName = latest.DeviceName,
            DeviceStatus = "未入台账",
            CommunicationState = "--",
            LatestStatus = latest.StatusText,
            LatestCheckedAt = latest.CheckedAtValue.ToString("MM-dd HH:mm"),
            Owner = latest.Inspector,
            PendingCount = pendingCount + (pendingCount == 0 ? 1 : 0),
            AttentionRank = abnormalCount > 0 ? 0 : warningCount > 0 ? 1 : 5,
            AttentionLevel = abnormalCount > 0
                ? "异常待处理"
                : warningCount > 0
                    ? "预警待确认"
                    : "未入台账"
        };
    }

    private static AttentionRow BuildDeviceAttentionRow(DeviceRowViewModel device)
    {
        if (device.Status != ManagedDeviceStatus.Active)
        {
            return new AttentionRow
            {
                DeviceName = device.DeviceName,
                InspectionItem = "设备状态",
                StatusText = device.Status == ManagedDeviceStatus.Stopped ? "异常" : "预警",
                CheckedAt = device.UpdatedAtText,
                Detail = $"{device.LineName} / {device.StatusText} / 台账"
            };
        }

        return new AttentionRow
        {
            DeviceName = device.DeviceName,
            InspectionItem = "通信配置",
            StatusText = "预警",
            CheckedAt = device.UpdatedAtText,
            Detail = $"{device.LineName} / 未配置通信地址"
        };
    }

    private static int GetAttentionRank(
        ManagedDeviceStatus status,
        bool isCommunicationConfigured,
        int abnormalCount,
        int warningCount)
    {
        if (abnormalCount > 0)
        {
            return 0;
        }

        if (warningCount > 0)
        {
            return 1;
        }

        if (status == ManagedDeviceStatus.Stopped)
        {
            return 2;
        }

        if (status == ManagedDeviceStatus.Maintenance)
        {
            return 3;
        }

        return isCommunicationConfigured ? 5 : 4;
    }

    private static string BuildDeviceKey(string lineName, string deviceName)
    {
        return $"{lineName.Trim()}|{deviceName.Trim()}".ToUpperInvariant();
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(
        string dataPropertyName,
        string headerText,
        float fillWeight,
        int minimumWidth = 68)
    {
        return new DataGridViewTextBoxColumn
        {
            DataPropertyName = dataPropertyName,
            HeaderText = headerText,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = fillWeight,
            MinimumWidth = minimumWidth
        };
    }

    private void DeviceGridOnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.Value is not string text)
        {
            return;
        }

        var propertyName = _deviceGrid.Columns[e.ColumnIndex].DataPropertyName;
        if (propertyName is nameof(DeviceRow.AttentionLevel) or nameof(DeviceRow.DeviceStatus) or nameof(DeviceRow.CommunicationState))
        {
            var cellStyle = e.CellStyle;
            if (cellStyle is null)
            {
                return;
            }

            cellStyle.ForeColor = text switch
            {
                "异常待处理" => DangerColor,
                "设备已停用" => DangerColor,
                "已停用" => DangerColor,
                "预警待确认" => WarningColor,
                "维护中" => WarningColor,
                "通信未配置" => WarningColor,
                "未配置" => WarningColor,
                "未入台账" => WarningColor,
                _ => SuccessColor
            };
        }
    }

    private void AttentionGridOnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_attentionGrid.Columns[e.ColumnIndex].DataPropertyName == nameof(AttentionRow.StatusText) &&
            e.Value is string text)
        {
            var cellStyle = e.CellStyle;
            if (cellStyle is null)
            {
                return;
            }

            cellStyle.ForeColor = text switch
            {
                "异常" => DangerColor,
                "预警" => WarningColor,
                _ => TextSecondaryColor
            };
        }
    }

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SurfaceBackground,
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            ScrollBars = ScrollBars.Vertical,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        PageChrome.ApplyGridTheme(grid);
        return grid;
    }

    private static Button CreateRefreshButton()
    {
        return PageChrome.CreateActionButton("刷新监控", AccentBlue, true);
    }

    private sealed class DeviceRow
    {
        public string LineName { get; init; } = string.Empty;

        public string DeviceName { get; init; } = string.Empty;

        public string DeviceStatus { get; init; } = string.Empty;

        public string CommunicationState { get; init; } = string.Empty;

        public string LatestStatus { get; init; } = string.Empty;

        public string AttentionLevel { get; init; } = string.Empty;

        public int AttentionRank { get; init; }

        public int PendingCount { get; init; }

        public string LatestCheckedAt { get; init; } = string.Empty;

        public string Owner { get; init; } = string.Empty;
    }

    private sealed class AttentionRow
    {
        public string DeviceName { get; init; } = string.Empty;

        public string InspectionItem { get; init; } = string.Empty;

        public string StatusText { get; init; } = string.Empty;

        public string CheckedAt { get; init; } = string.Empty;

        public string Detail { get; init; } = string.Empty;
    }
}
