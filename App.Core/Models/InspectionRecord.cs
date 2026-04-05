namespace App.Core.Models;

public sealed record InspectionRecord(
    Guid Id,
    string LineName,
    string DeviceName,
    string InspectionItem,
    string Inspector,
    InspectionStatus Status,
    decimal MeasuredValue,
    DateTime CheckedAt,
    string Remark,
    DateTime? ClosedAt = null,
    string? ClosedBy = null,
    string? ClosureRemark = null,
    bool IsRevoked = false,
    DateTime? RevokedAt = null,
    string? RevokedBy = null,
    string? RevokeReason = null);
