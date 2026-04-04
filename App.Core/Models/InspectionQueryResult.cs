namespace App.Core.Models;

public sealed record InspectionQueryResult(
    IReadOnlyList<InspectionRecord> Records,
    IReadOnlyList<string> LineOptions,
    InspectionSummary Summary,
    DateTime GeneratedAt);
