using App.Core.Models;

namespace App.WinForms.ViewModels;

internal sealed class InspectionDashboardViewModel
{
    public IReadOnlyList<string> LineOptions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<InspectionRecordViewModel> Records { get; init; } = Array.Empty<InspectionRecordViewModel>();

    public IReadOnlyList<InspectionTrendPointViewModel> TrendPoints { get; init; } = Array.Empty<InspectionTrendPointViewModel>();

    public int TotalCount { get; init; }

    public int NormalCount { get; init; }

    public int WarningCount { get; init; }

    public int AbnormalCount { get; init; }

    public string PassRateText { get; init; } = "0%";

    public DateTime GeneratedAt { get; init; }
}

internal sealed class InspectionRecordViewModel
{
    public string CheckedAt { get; init; } = string.Empty;

    public string LineName { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string InspectionItem { get; init; } = string.Empty;

    public string Inspector { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public string MeasuredValueText { get; init; } = string.Empty;

    public string Remark { get; init; } = string.Empty;
}

internal sealed class InspectionTrendPointViewModel
{
    public string Label { get; init; } = string.Empty;

    public int NormalCount { get; init; }

    public int WarningCount { get; init; }

    public int AbnormalCount { get; init; }
}

internal sealed class InspectionFilterViewModel
{
    public string Keyword { get; init; } = string.Empty;

    public string LineName { get; init; } = string.Empty;

    public InspectionStatus? Status { get; init; }

    public DateTime? StartTime { get; init; }

    public DateTime? EndTime { get; init; }
}

internal sealed class InspectionEntryViewModel
{
    public string LineName { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string InspectionItem { get; init; } = string.Empty;

    public string Inspector { get; init; } = string.Empty;

    public InspectionStatus Status { get; init; }

    public decimal MeasuredValue { get; init; }

    public DateTime CheckedAt { get; init; }

    public string Remark { get; init; } = string.Empty;
}
