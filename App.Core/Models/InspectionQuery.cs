namespace App.Core.Models;

public sealed record InspectionQuery(
    string Keyword,
    string LineName,
    InspectionStatus? Status,
    DateTime? StartTime,
    DateTime? EndTime);
