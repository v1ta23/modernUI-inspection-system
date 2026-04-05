using App.Core.Models;
using ClosedXML.Excel;

namespace App.WinForms.Exports;

internal sealed class InspectionExcelExporter
{
    public void Export(string filePath, InspectionQueryResult result)
    {
        using var workbook = new XLWorkbook();

        var summarySheet = workbook.Worksheets.Add("统计概览");
        summarySheet.Cell(1, 1).Value = "点检监控导出";
        summarySheet.Cell(2, 1).Value = "导出时间";
        summarySheet.Cell(2, 2).Value = result.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss");
        summarySheet.Cell(3, 1).Value = "记录总数";
        summarySheet.Cell(3, 2).Value = result.Summary.TotalCount;
        summarySheet.Cell(4, 1).Value = "正常";
        summarySheet.Cell(4, 2).Value = result.Summary.NormalCount;
        summarySheet.Cell(5, 1).Value = "预警";
        summarySheet.Cell(5, 2).Value = result.Summary.WarningCount;
        summarySheet.Cell(6, 1).Value = "异常";
        summarySheet.Cell(6, 2).Value = result.Summary.AbnormalCount;
        summarySheet.Cell(7, 1).Value = "合格率";
        summarySheet.Cell(7, 2).Value = $"{result.Summary.PassRate:0.0}%";
        summarySheet.Range("A1:B1").Merge();
        summarySheet.Cell(1, 1).Style.Font.Bold = true;
        summarySheet.Cell(1, 1).Style.Font.FontSize = 14;
        summarySheet.Columns().AdjustToContents();

        var trendSheet = workbook.Worksheets.Add("趋势图数据");
        trendSheet.Cell(1, 1).Value = "时间";
        trendSheet.Cell(1, 2).Value = "正常";
        trendSheet.Cell(1, 3).Value = "预警";
        trendSheet.Cell(1, 4).Value = "异常";
        for (var index = 0; index < result.Summary.TrendPoints.Count; index++)
        {
            var point = result.Summary.TrendPoints[index];
            var row = index + 2;
            trendSheet.Cell(row, 1).Value = point.Label;
            trendSheet.Cell(row, 2).Value = point.NormalCount;
            trendSheet.Cell(row, 3).Value = point.WarningCount;
            trendSheet.Cell(row, 4).Value = point.AbnormalCount;
        }
        trendSheet.Columns().AdjustToContents();

        var dataSheet = workbook.Worksheets.Add("点检记录");
        dataSheet.Cell(1, 1).Value = "点检时间";
        dataSheet.Cell(1, 2).Value = "产线";
        dataSheet.Cell(1, 3).Value = "设备名称";
        dataSheet.Cell(1, 4).Value = "点检项目";
        dataSheet.Cell(1, 5).Value = "点检人";
        dataSheet.Cell(1, 6).Value = "状态";
        dataSheet.Cell(1, 7).Value = "测量值";
        dataSheet.Cell(1, 8).Value = "闭环状态";
        dataSheet.Cell(1, 9).Value = "处理说明";
        dataSheet.Cell(1, 10).Value = "原始备注";

        for (var index = 0; index < result.Records.Count; index++)
        {
            var record = result.Records[index];
            var row = index + 2;
            dataSheet.Cell(row, 1).Value = record.CheckedAt.ToString("yyyy-MM-dd HH:mm");
            dataSheet.Cell(row, 2).Value = record.LineName;
            dataSheet.Cell(row, 3).Value = record.DeviceName;
            dataSheet.Cell(row, 4).Value = record.InspectionItem;
            dataSheet.Cell(row, 5).Value = record.Inspector;
            dataSheet.Cell(row, 6).Value = record.Status.ToDisplayText();
            dataSheet.Cell(row, 7).Value = record.MeasuredValue;
            dataSheet.Cell(row, 8).Value = BuildClosureStateText(record);
            dataSheet.Cell(row, 9).Value = BuildActionRemark(record);
            dataSheet.Cell(row, 10).Value = record.Remark;
        }

        var headerRange = dataSheet.Range(1, 1, 1, 10);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCEBFF");
        dataSheet.Columns().AdjustToContents();

        workbook.SaveAs(filePath);
    }

    private static string BuildClosureStateText(InspectionRecord record)
    {
        if (record.IsRevoked)
        {
            return "已撤回";
        }

        if (record.Status == InspectionStatus.Normal)
        {
            return "无需闭环";
        }

        return record.ClosedAt.HasValue
            ? $"已闭环 {record.ClosedAt:MM-dd HH:mm}"
            : "待闭环";
    }

    private static string BuildActionRemark(InspectionRecord record)
    {
        if (record.IsRevoked)
        {
            return record.RevokeReason ?? string.Empty;
        }

        return record.ClosureRemark ?? string.Empty;
    }
}
