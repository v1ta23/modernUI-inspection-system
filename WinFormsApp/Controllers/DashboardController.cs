using App.Core.Interfaces;
using App.Core.Models;
using WinFormsApp.ViewModels;
using System.Drawing;

namespace WinFormsApp.Controllers;

internal sealed class DashboardController
{
    private readonly IInspectionRecordService _inspectionRecordService;
    private readonly IManagedDeviceService _managedDeviceService;

    public DashboardController(
        IInspectionRecordService inspectionRecordService,
        IManagedDeviceService managedDeviceService)
    {
        _inspectionRecordService = inspectionRecordService;
        _managedDeviceService = managedDeviceService;
    }

    public DashboardViewModel Load(string account)
    {
        var result = _inspectionRecordService.Query(new InspectionQuery(
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            false));

        var records = result.Records
            .Where(record => !record.IsRevoked)
            .OrderByDescending(record => record.CheckedAt)
            .ToList();

        var todayStart = DateTime.Today;
        var todayRecords = records
            .Where(record => record.CheckedAt >= todayStart)
            .ToList();
        var pendingRecords = records
            .Where(record => record.Status != InspectionStatus.Normal && !record.ClosedAt.HasValue)
            .ToList();
        var deviceResult = _managedDeviceService.Query(new ManagedDeviceQuery(
            string.Empty,
            string.Empty,
            null));
        var devices = deviceResult.Devices;
        var deviceAttentionCount = devices.Count(device =>
            device.Status != ManagedDeviceStatus.Active ||
            string.IsNullOrWhiteSpace(device.CommunicationAddress));
        var maintenanceDeviceCount = devices.Count(device => device.Status == ManagedDeviceStatus.Maintenance);
        var stoppedDeviceCount = devices.Count(device => device.Status == ManagedDeviceStatus.Stopped);
        var activeDeviceCount = devices.Count(device => device.Status == ManagedDeviceStatus.Active);
        var todayNormalCount = todayRecords.Count(record => record.Status == InspectionStatus.Normal);
        var todayWarningCount = todayRecords.Count(record => record.Status == InspectionStatus.Warning);
        var todayPassRate = todayRecords.Count == 0
            ? 0m
            : Math.Round(todayNormalCount * 100m / todayRecords.Count, 1);

        return new DashboardViewModel
        {
            HeaderTitle = "首页",
            HeaderSubtitle = $"今日巡检 {todayRecords.Count} 条，设备台账 {devices.Count} 台，待处理 {pendingRecords.Count + deviceAttentionCount} 项。",
            Cards =
            [
                new DashboardCardViewModel
                {
                    Title = "今日巡检",
                    Value = todayRecords.Count.ToString(),
                    Detail = $"正常 {todayNormalCount} / 预警 {todayWarningCount}",
                    Icon = "巡检",
                    AccentColor = MapAccent("blue"),
                    NavigationTarget = DashboardNavigationTarget.InspectionToday
                },
                new DashboardCardViewModel
                {
                    Title = "设备台账",
                    Value = devices.Count.ToString(),
                    Detail = $"运行 {activeDeviceCount} / 维护 {maintenanceDeviceCount} / 停用 {stoppedDeviceCount}",
                    Icon = "设备",
                    AccentColor = MapAccent("cyan"),
                    NavigationTarget = DashboardNavigationTarget.DeviceManagement
                },
                new DashboardCardViewModel
                {
                    Title = "待处理告警",
                    Value = (pendingRecords.Count + deviceAttentionCount).ToString(),
                    Detail = $"巡检 {pendingRecords.Count} / 设备 {deviceAttentionCount}",
                    Icon = "告警",
                    AccentColor = MapAccent("pink"),
                    NavigationTarget = DashboardNavigationTarget.AlarmCenter
                },
                new DashboardCardViewModel
                {
                    Title = "今日合格率",
                    Value = $"{todayPassRate:0.0}%",
                    Detail = todayRecords.Count == 0
                        ? "今天还没有巡检记录"
                        : $"总数 {todayRecords.Count}，正常 {todayNormalCount}",
                    Icon = "合格率",
                    AccentColor = MapAccent("green"),
                    NavigationTarget = DashboardNavigationTarget.Analytics
                }
            ],
            Activities = records
                .Take(6)
                .Select(record => new DashboardActivityViewModel
                {
                    Time = record.CheckedAt.ToString("HH:mm"),
                    Text = $"{record.LineName} / {record.DeviceName} / {record.InspectionItem}",
                    Status = record.Status switch
                    {
                        InspectionStatus.Normal => "正常",
                        InspectionStatus.Warning => "预警",
                        InspectionStatus.Abnormal => "异常",
                        _ => "未知"
                    },
                    AccentColor = MapAccent(record.Status switch
                    {
                        InspectionStatus.Normal => "green",
                        InspectionStatus.Warning => "orange",
                        InspectionStatus.Abnormal => "pink",
                        _ => "blue"
                    })
                })
                .ToList(),
            QuickActions =
            [
                new DashboardQuickActionViewModel
                {
                    Text = "新增点检",
                    Icon = "新增",
                    PrimaryAccent = MapAccent("blue"),
                    SecondaryAccent = MapAccent("cyan"),
                    NavigationTarget = DashboardNavigationTarget.InspectionCreate
                },
                new DashboardQuickActionViewModel
                {
                    Text = "设备台账",
                    Icon = "台账",
                    PrimaryAccent = MapAccent("cyan"),
                    SecondaryAccent = MapAccent("blue"),
                    NavigationTarget = DashboardNavigationTarget.DeviceManagement
                },
                new DashboardQuickActionViewModel
                {
                    Text = "设备监控",
                    Icon = "监控",
                    PrimaryAccent = MapAccent("green"),
                    SecondaryAccent = MapAccent("cyan"),
                    NavigationTarget = DashboardNavigationTarget.DeviceMonitor
                },
                new DashboardQuickActionViewModel
                {
                    Text = "处理告警",
                    Icon = "告警",
                    PrimaryAccent = MapAccent("orange"),
                    SecondaryAccent = MapAccent("pink"),
                    NavigationTarget = DashboardNavigationTarget.AlarmCenter
                }
            ]
        };
    }

    private static Color MapAccent(string accent)
    {
        return accent.ToLowerInvariant() switch
        {
            "green" => Color.FromArgb(76, 217, 140),
            "orange" => Color.FromArgb(255, 165, 70),
            "purple" => Color.FromArgb(148, 90, 255),
            "pink" => Color.FromArgb(255, 100, 150),
            "cyan" => Color.FromArgb(50, 210, 220),
            _ => Color.FromArgb(88, 130, 255)
        };
    }
}
