using System.Drawing;

namespace WinFormsApp.ViewModels;

internal enum DashboardNavigationTarget
{
    None,
    InspectionToday,
    InspectionPending,
    InspectionAbnormal,
    InspectionCreate,
    DeviceManagement,
    DeviceMonitor,
    AlarmCenter,
    Analytics
}

internal sealed class DashboardViewModel
{
    public string HeaderTitle { get; init; } = string.Empty;

    public string HeaderSubtitle { get; init; } = string.Empty;

    public IReadOnlyList<DashboardCardViewModel> Cards { get; init; } = Array.Empty<DashboardCardViewModel>();

    public IReadOnlyList<DashboardActivityViewModel> Activities { get; init; } = Array.Empty<DashboardActivityViewModel>();

    public IReadOnlyList<DashboardQuickActionViewModel> QuickActions { get; init; } = Array.Empty<DashboardQuickActionViewModel>();
}

internal sealed class DashboardCardViewModel
{
    public string Title { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string Icon { get; init; } = string.Empty;

    public Color AccentColor { get; init; }

    public DashboardNavigationTarget NavigationTarget { get; init; }
}

internal sealed class DashboardActivityViewModel
{
    public string Time { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Color AccentColor { get; init; }
}

internal sealed class DashboardQuickActionViewModel
{
    public string Text { get; init; } = string.Empty;

    public string Icon { get; init; } = string.Empty;

    public Color PrimaryAccent { get; init; }

    public Color SecondaryAccent { get; init; }

    public DashboardNavigationTarget NavigationTarget { get; init; }
}
