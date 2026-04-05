namespace App.Core.Models;

public sealed record InspectionQueryResult(
    IReadOnlyList<InspectionRecord> Records,
    IReadOnlyList<string> LineOptions,
    IReadOnlyList<InspectionTemplate> Templates,
    InspectionSummary Summary,
    DateTime GeneratedAt);
