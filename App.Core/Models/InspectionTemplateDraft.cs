namespace App.Core.Models;

public sealed record InspectionTemplateDraft(
    string LineName,
    string DeviceName,
    string InspectionItem,
    string DefaultInspector,
    string DefaultRemark,
    Guid? Id = null);
