namespace App.Core.Models;

public static class InspectionStatusExtensions
{
    public static string ToDisplayText(this InspectionStatus status)
    {
        return status switch
        {
            InspectionStatus.Normal => "正常",
            InspectionStatus.Warning => "预警",
            InspectionStatus.Abnormal => "异常",
            _ => "未知"
        };
    }
}
