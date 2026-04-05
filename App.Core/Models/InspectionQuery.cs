namespace App.Core.Models;

public sealed record InspectionQuery(
    string Keyword,
    string LineName,
    string DeviceName,
    InspectionStatus? Status,
    DateTime? StartTime,
    DateTime? EndTime,
    bool IncludeRevoked)
{
    public InspectionQuery(
        string keyword,
        string lineName,
        InspectionStatus? status,
        DateTime? startTime,
        DateTime? endTime,
        bool includeRevoked)
        : this(keyword, lineName, string.Empty, status, startTime, endTime, includeRevoked)
    {
    }
}
