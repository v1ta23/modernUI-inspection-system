using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Net.Sockets;
using System.Text;

namespace WinFormsApp.Views;

internal sealed class CommunicationDemoPageControl : UserControl, IInteractiveResizeAware
{
    private readonly BindingList<DeviceStatusRow> _deviceRows = new();
    private readonly BindingList<PacketLogRow> _packetRows = new();
    private readonly Control _layoutRoot;
    private readonly InteractiveResizeFreezeController _interactiveResizeController;
    private readonly Label _infoLabel;
    private readonly Label _connectionValueLabel;
    private readonly Label _onlineValueLabel;
    private readonly Label _latencyValueLabel;
    private readonly Label _alarmValueLabel;
    private readonly Label _connectionNoteLabel;
    private readonly Label _onlineNoteLabel;
    private readonly Label _latencyNoteLabel;
    private readonly Label _alarmNoteLabel;
    private readonly Button _connectButton;
    private readonly TextBox _sendTextBox;
    private readonly TextBox _responseTextBox;
    private readonly TopologyCanvas _topologyCanvas;
    private readonly DataGridView _deviceGrid;
    private readonly DataGridView _packetGrid;
    private TcpClient? _tcpClient;
    private NetworkStream? _tcpStream;

    private bool _connected;
    private int _pulse;
    private int _alarmCount;
    private int _messageCount;
    private int _replyCount;
    private int _lastDeviceIndex = -1;
    private string _deviceHost = "127.0.0.1";
    private int _devicePort = 9001;
    private string _lastFlowText = "还没开始";

    public CommunicationDemoPageControl()
    {
        Dock = DockStyle.Fill;
        BackColor = PageChrome.PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);

        _infoLabel = PageChrome.CreateInfoLabel("点击 1 打开连接窗口，连接后发送测试内容并读取真实回复。");
        _connectionValueLabel = PageChrome.CreateValueLabel(18F, "未连接");
        _onlineValueLabel = PageChrome.CreateValueLabel(18F, "0/1");
        _latencyValueLabel = PageChrome.CreateValueLabel(18F, "暂无");
        _alarmValueLabel = PageChrome.CreateValueLabel(18F, "0");
        _connectionNoteLabel = PageChrome.CreateNoteLabel("连接后才能发送测试消息");
        _onlineNoteLabel = PageChrome.CreateNoteLabel("当前没有设备在线");
        _latencyNoteLabel = PageChrome.CreateNoteLabel("最近做了什么会显示在这里");
        _alarmNoteLabel = PageChrome.CreateNoteLabel("设备故障会写入通信记录");

        _sendTextBox = CreateTextInput("PING");
        _responseTextBox = CreateTextInput("真实设备回复会显示在这里");
        _responseTextBox.Multiline = true;
        _responseTextBox.ReadOnly = true;
        _responseTextBox.ScrollBars = ScrollBars.Vertical;

        _connectButton = PageChrome.CreateActionButton("1 连接设备", PageChrome.AccentGreen, true);
        var sendButton = PageChrome.CreateActionButton("2 发送测试", PageChrome.AccentBlue, false);
        var replyButton = PageChrome.CreateActionButton("3 接收回复", PageChrome.AccentCyan, false);
        var errorButton = PageChrome.CreateActionButton("4 模拟故障", PageChrome.AccentOrange, false);
        var clearButton = PageChrome.CreateActionButton("清空记录", PageChrome.AccentPurple, false);

        _connectButton.Click += (_, _) => ToggleRealConnection();
        sendButton.Click += (_, _) => SendTestMessage();
        replyButton.Click += (_, _) => ReadRealReply();
        errorButton.Click += (_, _) => SimulateError();
        clearButton.Click += (_, _) => ClearRecords();

        _topologyCanvas = new TopologyCanvas(_deviceRows)
        {
            Dock = DockStyle.Fill
        };

        _deviceGrid = CreateGrid();
        _packetGrid = CreateGrid();
        ConfigureDeviceGrid();
        ConfigurePacketGrid();
        ConfigureGridFormatting();

        SeedDemoData();
        _layoutRoot = BuildLayout(_connectButton, sendButton, replyButton, errorButton, clearButton);
        Controls.Add(_layoutRoot);
        _interactiveResizeController = new InteractiveResizeFreezeController(this, _layoutRoot, PageChrome.PageBackground);
        _layoutRoot.BringToFront();
        RefreshStatus();
    }

    public void ApplyTheme()
    {
        BackColor = PageChrome.PageBackground;
        PageChrome.ApplyGridTheme(_deviceGrid);
        PageChrome.ApplyGridTheme(_packetGrid);
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
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseTcpConnection();
        }

        base.Dispose(disposing);
    }

    private Control BuildLayout(
        Button connectButton,
        Button sendButton,
        Button replyButton,
        Button errorButton,
        Button clearButton)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, PageChrome.HeaderHeight + 12));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = PageChrome.CreatePageHeader(
            "通信测试台",
            "连接真实 TCP 设备，发送测试内容，并查看设备返回的原始回复。",
            _infoLabel,
            clearButton,
            errorButton,
            replyButton,
            sendButton,
            connectButton);
        PageChrome.BindControlHeightToRow(root, 0, header);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(BuildMetrics(), 0, 1);
        root.Controls.Add(BuildWorkspace(), 0, 2);
        return root;
    }

    private Control BuildMetrics()
    {
        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty
        };
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (var index = 0; index < 4; index++)
        {
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        metrics.Controls.Add(PageChrome.CreateMetricCard("连接状态", PageChrome.AccentGreen, _connectionValueLabel, _connectionNoteLabel), 0, 0);
        metrics.Controls.Add(PageChrome.CreateMetricCard("已连设备", PageChrome.AccentCyan, _onlineValueLabel, _onlineNoteLabel), 1, 0);
        metrics.Controls.Add(PageChrome.CreateMetricCard("最新动作", PageChrome.AccentBlue, _latencyValueLabel, _latencyNoteLabel), 2, 0);
        metrics.Controls.Add(PageChrome.CreateMetricCard("故障次数", PageChrome.AccentOrange, _alarmValueLabel, _alarmNoteLabel, new Padding(0)), 3, 0);
        return metrics;
    }

    private Control BuildSendPanel()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        body.Controls.Add(CreateFieldLabel("发送内容"), 0, 0);
        body.Controls.Add(_sendTextBox, 1, 0);
        body.Controls.Add(CreateFieldLabel("设备回复"), 0, 1);
        body.Controls.Add(_responseTextBox, 1, 1);

        return PageChrome.CreateSectionShell(
            "发送与回复",
            "连接后发送一条测试内容，设备原始回复用于判断通信是否正常。",
            out _,
            body,
            new Padding(0, 0, 12, 12));
    }

    private Control BuildWorkspace()
    {
        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var leftSide = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        leftSide.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        leftSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 168F));
        leftSide.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var topologyShell = PageChrome.CreateSectionShell(
            "控制端与设备",
            "中间是控制端，外侧是当前待测设备。高亮连线表示最近一次通信。",
            out _,
            _topologyCanvas,
            new Padding(0, 0, 12, 0));

        leftSide.Controls.Add(BuildSendPanel(), 0, 0);
        leftSide.Controls.Add(topologyShell, 0, 1);

        var rightSide = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        rightSide.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rightSide.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
        rightSide.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
        rightSide.Controls.Add(PageChrome.CreateSectionShell("设备列表", "设备状态分为在线、离线、故障。", out _, _deviceGrid), 0, 0);
        rightSide.Controls.Add(PageChrome.CreateSectionShell("通信记录", "记录每一步通信动作和处理结果。", out _, _packetGrid, new Padding(0)), 0, 1);

        workspace.Controls.Add(leftSide, 0, 0);
        workspace.Controls.Add(rightSide, 1, 0);
        return workspace;
    }

    private void SeedDemoData()
    {
        _deviceRows.Add(new DeviceStatusRow("待测设备", "离线", "0 次", "--", "等待连接"));

        _deviceGrid.DataSource = _deviceRows;
        _packetGrid.DataSource = _packetRows;
        AddPacket("系统", "控制端", "页面已就绪，请先连接设备。", "提示");
    }

    private void ConfigureDeviceGrid()
    {
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.DeviceName), "设备", 100));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.LinkState), "状态", 76));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.Latency), "次数", 70));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.LastPacket), "时间", 92));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.Detail), "说明", 140));
    }

    private void ConfigurePacketGrid()
    {
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Time), "时间", 78));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Direction), "动作", 70));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.DeviceName), "对象", 92));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Payload), "说明", 180));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Result), "状态", 74));
    }

    private void ConfigureGridFormatting()
    {
        _deviceGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 ||
                e.ColumnIndex < 0 ||
                e.CellStyle is not { } style ||
                _deviceGrid.Rows[e.RowIndex].DataBoundItem is not DeviceStatusRow row)
            {
                return;
            }

            var stateColor = GetStateColor(row.LinkState);
            var columnName = _deviceGrid.Columns[e.ColumnIndex].DataPropertyName;
            if (columnName == nameof(DeviceStatusRow.LinkState))
            {
                style.ForeColor = stateColor;
                style.SelectionForeColor = stateColor;
            }
            else if (row.LinkState is "故障" or "错误" or "异常" or "超时")
            {
                style.ForeColor = PageChrome.MixColor(PageChrome.TextSecondary, stateColor, 0.32F);
            }
        };

        _packetGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 ||
                e.ColumnIndex < 0 ||
                e.CellStyle is not { } style ||
                _packetGrid.Rows[e.RowIndex].DataBoundItem is not PacketLogRow row)
            {
                return;
            }

            var columnName = _packetGrid.Columns[e.ColumnIndex].DataPropertyName;
            if (columnName == nameof(PacketLogRow.Direction))
            {
                var directionColor = row.Direction switch
                {
                    "发送" => PageChrome.AccentBlue,
                    "接收" => PageChrome.AccentCyan,
                    "故障" => PageChrome.AccentOrange,
                    _ => PageChrome.AccentPurple
                };
                style.ForeColor = directionColor;
                style.SelectionForeColor = directionColor;
            }
            else if (columnName == nameof(PacketLogRow.Result))
            {
                var resultColor = GetResultColor(row.Result);
                style.ForeColor = resultColor;
                style.SelectionForeColor = resultColor;
            }
        };
    }

    private static DataGridView CreateGrid()
    {
        var grid = new BufferedDataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            ScrollBars = ScrollBars.Vertical,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        PageChrome.ApplyGridTheme(grid);
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        return grid;
    }

    private static DataGridViewTextBoxColumn CreateColumn(string propertyName, string headerText, float fillWeight)
    {
        return new DataGridViewTextBoxColumn
        {
            DataPropertyName = propertyName,
            HeaderText = headerText,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = fillWeight,
            MinimumWidth = 58
        };
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            ForeColor = PageChrome.TextSecondary,
            Margin = Padding.Empty,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static TextBox CreateTextInput(string text)
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = PageChrome.InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = PageChrome.TextPrimary,
            Margin = new Padding(0, 0, 10, 8),
            Text = text
        };
    }

    private static Color GetStateColor(string state)
    {
        return state switch
        {
            "在线" => PageChrome.AccentGreen,
            "故障" or "错误" or "异常" => PageChrome.AccentOrange,
            "超时" => PageChrome.AccentRed,
            _ => PageChrome.TextMuted
        };
    }

    private static Color GetResultColor(string result)
    {
        return result switch
        {
            "成功" or "正常" or "已发送" or "已收到" or "已清空" => PageChrome.AccentGreen,
            "故障" or "错误" or "异常" => PageChrome.AccentOrange,
            "超时" or "未接收" => PageChrome.AccentRed,
            "未发送" or "提示" or "跳过" => PageChrome.TextMuted,
            _ => PageChrome.TextSecondary
        };
    }

    private void ToggleRealConnection()
    {
        if (_connected)
        {
            DisconnectRealDevice("连接已断开，当前不能发送消息。");
            return;
        }

        using var dialog = new ConnectionDialog(_deviceHost, _devicePort);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        _deviceHost = dialog.DeviceHost;
        _devicePort = dialog.DevicePort;
        ConnectToDevice();
    }

    private void ConnectToDevice()
    {
        _connectButton.Enabled = false;
        _responseTextBox.Text = "正在连接...";
        try
        {
            var client = new TcpClient();
            client.Connect(_deviceHost, _devicePort);
            _tcpClient = client;
            _tcpStream = client.GetStream();
            _tcpStream.ReadTimeout = 3000;
            _connected = true;
            _connectButton.Text = "断开连接";
            _lastDeviceIndex = 0;
            _lastFlowText = "已连接";
            UpdateDeviceRow("在线", "0 次", $"已连接 {_deviceHost}:{_devicePort}");
            AddPacket("系统", "控制端", $"已连接 {_deviceHost}:{_devicePort}。", "成功");
            _responseTextBox.Text = "连接成功。请输入发送内容，然后点击 2 发送测试。";
        }
        catch (Exception ex)
        {
            CloseTcpConnection();
            _connected = false;
            _lastFlowText = "连接失败";
            UpdateDeviceRow("离线", "0 次", "连接失败");
            AddPacket("故障", "控制端", $"连接失败：{ex.Message}", "故障");
            _responseTextBox.Text = $"连接失败：{ex.Message}";
        }
        finally
        {
            _connectButton.Enabled = true;
            RefreshStatus();
        }
    }

    private void SendTestMessage()
    {
        if (!_connected || _tcpStream is null)
        {
            AddPacket("发送", "控制端", "请先连接设备，再发送测试内容。", "未发送");
            _responseTextBox.Text = "请先连接设备。";
            return;
        }

        var text = _sendTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            AddPacket("发送", "控制端", "发送内容不能为空。", "未发送");
            _responseTextBox.Text = "发送内容不能为空。";
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(text + Environment.NewLine);
            _tcpStream.Write(bytes, 0, bytes.Length);
            _tcpStream.Flush();
            _messageCount++;
            _lastDeviceIndex = 0;
            _lastFlowText = "发送";
            UpdateDeviceRow("在线", $"{_messageCount} 次", "已发送测试内容");
            AddPacket("发送", "待测设备", $"控制端发送：{Shorten(text)}", "已发送");
        }
        catch (Exception ex)
        {
            AddPacket("故障", "待测设备", $"发送失败：{ex.Message}", "故障");
            _responseTextBox.Text = $"发送失败：{ex.Message}";
            DisconnectRealDevice("发送失败，连接已关闭。", addLog: false);
        }
        finally
        {
            RefreshStatus();
        }
    }

    private void ReadRealReply()
    {
        if (!_connected || _tcpStream is null)
        {
            AddPacket("接收", "控制端", "请先连接设备，再读取回复。", "未接收");
            _responseTextBox.Text = "请先连接设备。";
            return;
        }

        try
        {
            var buffer = new byte[4096];
            var count = _tcpStream.Read(buffer, 0, buffer.Length);
            if (count <= 0)
            {
                DisconnectRealDevice("设备已关闭连接。");
                return;
            }

            var response = Encoding.UTF8.GetString(buffer, 0, count).TrimEnd();
            _replyCount++;
            _lastDeviceIndex = 0;
            _lastFlowText = "接收";
            _responseTextBox.Text = response;
            UpdateDeviceRow("在线", $"{Math.Max(_messageCount, _replyCount)} 次", "已收到真实回复");
            AddPacket("接收", "待测设备", $"真实回复：{Shorten(response)}", "已收到");
        }
        catch (IOException)
        {
            _lastFlowText = "超时";
            _responseTextBox.Text = "3 秒内没有收到设备回复。";
            AddPacket("接收", "待测设备", "3 秒内没有收到设备回复。", "超时");
        }
        catch (Exception ex)
        {
            _responseTextBox.Text = $"读取失败：{ex.Message}";
            AddPacket("故障", "待测设备", $"读取失败：{ex.Message}", "故障");
            DisconnectRealDevice("读取失败，连接已关闭。", addLog: false);
        }
        finally
        {
            RefreshStatus();
        }
    }

    private void SimulateError()
    {
        if (!_connected)
        {
            AddPacket("故障", "控制端", "请先连接设备，再模拟故障。", "未接收");
            return;
        }

        var device = _deviceRows[0];
        device.LinkState = "故障";
        device.Latency = "故障";
        device.LastPacket = DateTime.Now.ToString("HH:mm:ss");
        device.Detail = "压力超过安全范围";
        _alarmCount++;
        AddPacket("故障", device.DeviceName, "设备故障：压力超过安全范围，请检查。", "故障");
        RefreshStatus();
    }

    private void ClearRecords()
    {
        _packetRows.Clear();
        _messageCount = 0;
        _replyCount = 0;
        _alarmCount = 0;
        _lastDeviceIndex = -1;
        _lastFlowText = "已清空";
        _responseTextBox.Text = _connected ? "通信记录已清空，可以重新发送测试内容。" : "通信记录已清空。";

        foreach (var device in _deviceRows)
        {
            device.LinkState = _connected ? "在线" : "离线";
            device.Latency = "0 次";
            device.LastPacket = _connected ? DateTime.Now.ToString("HH:mm:ss") : "--";
            device.Detail = _connected ? "可以收发消息" : "等待连接";
        }

        AddPacket("系统", "控制端", "通信记录已清空，可以重新演示。", "已清空");
        RefreshStatus();
    }

    private void UpdateDeviceRow(string state, string countText, string detail)
    {
        if (_deviceRows.Count == 0)
        {
            return;
        }

        var device = _deviceRows[0];
        device.LinkState = state;
        device.Latency = countText;
        device.LastPacket = state == "离线" ? "--" : DateTime.Now.ToString("HH:mm:ss");
        device.Detail = detail;
    }

    private void DisconnectRealDevice(string message, bool addLog = true)
    {
        CloseTcpConnection();
        _connected = false;
        _connectButton.Text = "1 连接设备";
        _lastDeviceIndex = -1;
        _lastFlowText = "已断开";
        UpdateDeviceRow("离线", "0 次", "连接已断开");
        _responseTextBox.Text = message;
        if (addLog)
        {
            AddPacket("系统", "控制端", message, "成功");
        }

        RefreshStatus();
    }

    private void CloseTcpConnection()
    {
        _tcpStream?.Dispose();
        _tcpClient?.Close();
        _tcpClient?.Dispose();
        _tcpStream = null;
        _tcpClient = null;
    }

    private static string Shorten(string text)
    {
        var value = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= 80 ? value : value[..80] + "...";
    }

    private void AddPacket(string direction, string deviceName, string payload, string result)
    {
        var deviceIndex = FindDeviceIndex(deviceName);
        if (deviceIndex >= 0)
        {
            _lastDeviceIndex = deviceIndex;
        }

        _pulse++;
        _lastFlowText = direction;
        _packetRows.Insert(0, new PacketLogRow(
            DateTime.Now.ToString("HH:mm:ss"),
            direction,
            deviceName,
            payload,
            result));

        while (_packetRows.Count > 80)
        {
            _packetRows.RemoveAt(_packetRows.Count - 1);
        }
    }

    private int FindDeviceIndex(string deviceName)
    {
        for (var index = 0; index < _deviceRows.Count; index++)
        {
            if (_deviceRows[index].DeviceName == deviceName)
            {
                return index;
            }
        }

        return -1;
    }

    private void RefreshStatus()
    {
        var onlineCount = _deviceRows.Count(row => row.LinkState == "在线" || row.LinkState == "故障");
        var issueCount = _deviceRows.Count(row => row.LinkState == "故障");

        _connectionValueLabel.Text = _connected ? "已连接" : "未连接";
        _connectionValueLabel.ForeColor = _connected ? PageChrome.AccentGreen : PageChrome.TextPrimary;
        _connectionNoteLabel.Text = _connected ? $"已连接 {_deviceHost}:{_devicePort}" : "连接后才能发送测试消息";
        _onlineValueLabel.Text = $"{onlineCount}/{_deviceRows.Count}";
        _onlineNoteLabel.Text = issueCount > 0
            ? $"{issueCount} 台设备处于故障状态"
            : _connected ? $"当前设备 {_deviceHost}:{_devicePort}" : "当前没有设备在线";
        _latencyValueLabel.Text = _lastFlowText;
        _latencyNoteLabel.Text = _connected
            ? $"已发送 {_messageCount} 条，已收到 {_replyCount} 条"
            : "最近做了什么会显示在这里";
        _alarmValueLabel.Text = _alarmCount.ToString();
        _alarmNoteLabel.Text = _alarmCount == 0
            ? "设备故障会写入通信记录"
            : $"已记录 {_alarmCount} 次设备故障";
        _infoLabel.Text = _connected
            ? "请继续按 2 发送测试、按 3 接收回复；需要演示故障时按 4。"
            : "请先按 1 连接设备。";

        _deviceRows.ResetBindings();
        _topologyCanvas.Connected = _connected;
        _topologyCanvas.Pulse = _pulse;
        _topologyCanvas.ActiveDeviceIndex = _lastDeviceIndex;
        _topologyCanvas.FlowText = _lastFlowText;
        _topologyCanvas.Invalidate();
    }

    private sealed class DeviceStatusRow
    {
        public DeviceStatusRow(string deviceName, string linkState, string latency, string lastPacket, string detail)
        {
            DeviceName = deviceName;
            LinkState = linkState;
            Latency = latency;
            LastPacket = lastPacket;
            Detail = detail;
        }

        public string DeviceName { get; }

        public string LinkState { get; set; }

        public string Latency { get; set; }

        public string LastPacket { get; set; }

        public string Detail { get; set; }
    }

    private sealed class PacketLogRow
    {
        public PacketLogRow(string time, string direction, string deviceName, string payload, string result)
        {
            Time = time;
            Direction = direction;
            DeviceName = deviceName;
            Payload = payload;
            Result = result;
        }

        public string Time { get; }

        public string Direction { get; }

        public string DeviceName { get; }

        public string Payload { get; }

        public string Result { get; }
    }

    private sealed class BufferedDataGridView : DataGridView
    {
        public BufferedDataGridView()
        {
            DoubleBuffered = true;
        }
    }

    private sealed class ConnectionDialog : Form
    {
        private readonly TextBox _hostTextBox;
        private readonly TextBox _portTextBox;

        public ConnectionDialog(string host, int port)
        {
            Text = "连接设备";
            Size = new Size(420, 230);
            MinimumSize = new Size(420, 230);
            MaximumSize = new Size(520, 260);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = PageChrome.PageBackground;
            Font = new Font("Microsoft YaHei UI", 9F);

            _hostTextBox = CreateDialogTextBox(host);
            _hostTextBox.Name = "HostTextBox";
            _portTextBox = CreateDialogTextBox(port.ToString());
            _portTextBox.Name = "PortTextBox";

            var connectButton = PageChrome.CreateActionButton("连接", PageChrome.AccentGreen, true);
            connectButton.AutoSize = false;
            connectButton.Size = new Size(86, 36);
            connectButton.Click += (_, _) => Confirm();

            var cancelButton = PageChrome.CreateActionButton("取消", PageChrome.SurfaceBorder, false);
            cancelButton.AutoSize = false;
            cancelButton.Size = new Size(86, 36);
            cancelButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            AcceptButton = connectButton;
            CancelButton = cancelButton;

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            actions.Controls.Add(connectButton);
            actions.Controls.Add(cancelButton);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(20, 18, 20, 18)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var tipLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = PageChrome.TextMuted,
                Text = "请输入 TCP 设备的地址和端口。",
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(tipLabel, 0, 0);
            layout.SetColumnSpan(tipLabel, 2);
            layout.Controls.Add(CreateDialogLabel("设备地址"), 0, 1);
            layout.Controls.Add(_hostTextBox, 1, 1);
            layout.Controls.Add(CreateDialogLabel("端口"), 0, 2);
            layout.Controls.Add(_portTextBox, 1, 2);
            layout.Controls.Add(actions, 0, 3);
            layout.SetColumnSpan(actions, 2);

            Controls.Add(layout);
        }

        public string DeviceHost { get; private set; } = string.Empty;

        public int DevicePort { get; private set; }

        private void Confirm()
        {
            var host = _hostTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "请输入设备地址。", "连接设备", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!int.TryParse(_portTextBox.Text.Trim(), out var port))
            {
                MessageBox.Show(this, "端口必须是数字。", "连接设备", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DeviceHost = host;
            DevicePort = port;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static Label CreateDialogLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = PageChrome.TextSecondary,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static TextBox CreateDialogTextBox(string text)
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = PageChrome.InputBackground,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = PageChrome.TextPrimary,
                Margin = new Padding(0, 4, 0, 8),
                Text = text
            };
        }
    }

    private sealed class TopologyCanvas : Control
    {
        private readonly IReadOnlyList<DeviceStatusRow> _devices;

        public TopologyCanvas(IReadOnlyList<DeviceStatusRow> devices)
        {
            _devices = devices;
            BackColor = PageChrome.SurfaceBackground;
            MinimumSize = new Size(360, 300);
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            DoubleBuffered = true;
            UpdateStyles();
        }

        public bool Connected { get; set; }

        public int Pulse { get; set; }

        public int ActiveDeviceIndex { get; set; } = -1;

        public string FlowText { get; set; } = "等待连接";

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            using var brush = new SolidBrush(PageChrome.SurfaceBackground);
            pevent.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawAtmosphere(g);
            DrawGateway(g);
            DrawDevices(g);
            DrawLegend(g);
        }

        private void DrawAtmosphere(Graphics g)
        {
            var bounds = ClientRectangle;
            using var glowBrush = new LinearGradientBrush(
                bounds,
                Color.FromArgb(34, PageChrome.AccentBlue),
                Color.FromArgb(4, PageChrome.AccentCyan),
                LinearGradientMode.ForwardDiagonal);
            g.FillRectangle(glowBrush, bounds);

            for (var index = 0; index < 5; index++)
            {
                var size = Math.Max(80, Math.Min(Width, Height) - index * 52);
                var rect = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
                using var pen = new Pen(Color.FromArgb(Connected ? 20 : 8, PageChrome.AccentBlue), 1F);
                g.DrawEllipse(pen, rect);
            }
        }

        private void DrawGateway(Graphics g)
        {
            var center = new Point(Width / 2, Height / 2);
            var radius = Math.Max(46, Math.Min(74, Math.Min(Width, Height) / 7));
            var gatewayRect = new Rectangle(center.X - radius, center.Y - radius, radius * 2, radius * 2);

            using var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
            g.FillEllipse(shadowBrush, Rectangle.Inflate(gatewayRect, 10, 10));

            using var fillBrush = new LinearGradientBrush(
                gatewayRect,
                Color.FromArgb(58, 70, 96),
                Color.FromArgb(22, 26, 38),
                90F);
            g.FillEllipse(fillBrush, gatewayRect);

            using var borderPen = new Pen(Connected ? PageChrome.AccentGreen : PageChrome.SurfaceBorder, 2F);
            g.DrawEllipse(borderPen, gatewayRect);

            if (Connected)
            {
                var pulseSize = radius * 2 + 16 + (Pulse % 4) * 9;
                var pulseRect = new Rectangle(center.X - pulseSize / 2, center.Y - pulseSize / 2, pulseSize, pulseSize);
                using var pulsePen = new Pen(Color.FromArgb(86 - (Pulse % 4) * 18, PageChrome.AccentGreen), 2F);
                g.DrawEllipse(pulsePen, pulseRect);
            }

            using var titleFont = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            using var noteFont = new Font("Microsoft YaHei UI", 8.5F);
            using var titleBrush = new SolidBrush(PageChrome.TextPrimary);
            using var noteBrush = new SolidBrush(PageChrome.TextMuted);
            DrawCenteredText(g, "控制端", titleFont, titleBrush, new Rectangle(gatewayRect.X, gatewayRect.Y + 34, gatewayRect.Width, 26));
            DrawCenteredText(g, Connected ? "已连接" : "未连接", noteFont, noteBrush, new Rectangle(gatewayRect.X, gatewayRect.Y + 60, gatewayRect.Width, 24));
        }

        private void DrawDevices(Graphics g)
        {
            if (_devices.Count == 0)
            {
                return;
            }

            var center = new Point(Width / 2, Height / 2);
            var layoutRadiusX = Math.Max(130, Width / 2 - 110);
            var layoutRadiusY = Math.Max(96, Height / 2 - 82);
            for (var index = 0; index < _devices.Count; index++)
            {
                var angle = -Math.PI / 2 + index * (Math.PI * 2 / _devices.Count);
                var nodeCenter = new Point(
                    center.X + (int)(Math.Cos(angle) * layoutRadiusX),
                    center.Y + (int)(Math.Sin(angle) * layoutRadiusY));

                var stateColor = _devices[index].LinkState switch
                {
                    "在线" => PageChrome.AccentGreen,
                    "故障" or "错误" or "异常" => PageChrome.AccentOrange,
                    "超时" => PageChrome.AccentRed,
                    _ => PageChrome.SurfaceBorder
                };

                var active = index == ActiveDeviceIndex;
                using var linePen = new Pen(Color.FromArgb(active ? 180 : Connected ? 92 : 32, stateColor), active ? 3.2F : 2F);
                g.DrawLine(linePen, center, nodeCenter);
                if (active && Connected)
                {
                    DrawFlowTag(g, center, nodeCenter, FlowText, stateColor);
                }

                DrawDeviceNode(g, nodeCenter, _devices[index], stateColor, active);
            }
        }

        private static void DrawDeviceNode(Graphics g, Point center, DeviceStatusRow device, Color stateColor, bool active)
        {
            const int width = 132;
            const int height = 58;
            var rect = new Rectangle(center.X - width / 2, center.Y - height / 2, width, height);
            using var path = PageChrome.CreateRoundedPath(rect, 18);

            using var shadowBrush = new SolidBrush(Color.FromArgb(64, 0, 0, 0));
            using var shadowPath = PageChrome.CreateRoundedPath(new Rectangle(rect.X + 2, rect.Y + 5, rect.Width, rect.Height), 18);
            g.FillPath(shadowBrush, shadowPath);

            using var fillBrush = new SolidBrush(Color.FromArgb(238, 25, 29, 41));
            using var tintBrush = new SolidBrush(Color.FromArgb(device.LinkState == "离线" ? 10 : 24, stateColor));
            using var borderPen = new Pen(Color.FromArgb(active ? 220 : device.LinkState == "离线" ? 76 : 150, stateColor), active ? 2F : 1.3F);
            g.FillPath(fillBrush, path);
            g.FillPath(tintBrush, path);
            g.DrawPath(borderPen, path);

            using var nameFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            using var detailFont = new Font("Microsoft YaHei UI", 8F);
            using var nameBrush = new SolidBrush(PageChrome.TextPrimary);
            using var detailBrush = new SolidBrush(PageChrome.TextMuted);
            g.DrawString(device.DeviceName, nameFont, nameBrush, rect.X + 12, rect.Y + 10);
            g.DrawString($"{device.LinkState} / {device.Latency}", detailFont, detailBrush, rect.X + 12, rect.Y + 32);
        }

        private static void DrawFlowTag(Graphics g, Point from, Point to, string text, Color accent)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var x = (from.X + to.X) / 2;
            var y = (from.Y + to.Y) / 2;
            var rect = new Rectangle(x - 42, y - 13, 84, 26);
            using var path = PageChrome.CreateRoundedPath(rect, 10);
            using var fillBrush = new SolidBrush(Color.FromArgb(230, 18, 22, 31));
            using var borderPen = new Pen(Color.FromArgb(150, accent), 1F);
            using var font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold);
            using var brush = new SolidBrush(accent);
            g.FillPath(fillBrush, path);
            g.DrawPath(borderPen, path);
            DrawCenteredText(g, text, font, brush, rect);
        }

        private void DrawLegend(Graphics g)
        {
            using var font = new Font("Microsoft YaHei UI", 8.5F);
            using var brush = new SolidBrush(PageChrome.TextMuted);
            g.DrawString("Demo: 控制端发送测试消息，设备返回确认；故障设备会变色并写入记录。", font, brush, 18, Height - 32);
        }

        private static void DrawCenteredText(Graphics g, string text, Font font, Brush brush, Rectangle bounds)
        {
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };
            g.DrawString(text, font, brush, bounds, format);
        }
    }
}
