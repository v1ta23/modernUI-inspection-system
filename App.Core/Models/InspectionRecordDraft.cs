namespace App.Core.Models;

public sealed record InspectionRecordDraft(
    string LineName,
    string DeviceName,
    string InspectionItem,
    string Inspector,
    InspectionStatus Status,
    decimal MeasuredValue,
    DateTime CheckedAt,
    string Remark);
