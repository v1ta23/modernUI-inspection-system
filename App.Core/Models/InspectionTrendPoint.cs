namespace App.Core.Models;

public sealed record InspectionTrendPoint(
    string Label,
    int NormalCount,
    int WarningCount,
    int AbnormalCount);
