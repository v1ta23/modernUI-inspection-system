namespace App.Core.Models;

public sealed record InspectionSummary(
    int TotalCount,
    int NormalCount,
    int WarningCount,
    int AbnormalCount,
    decimal PassRate,
    IReadOnlyList<InspectionTrendPoint> TrendPoints);
