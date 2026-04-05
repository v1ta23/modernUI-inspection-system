namespace App.Core.Models;

public sealed record InspectionTemplate(
    Guid Id,
    string LineName,
    string DeviceName,
    string InspectionItem,
    string DefaultInspector,
    string DefaultRemark);
