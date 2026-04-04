using App.Core.Interfaces;
using App.Core.Models;
using App.WinForms.Exports;
using App.WinForms.ViewModels;

namespace App.WinForms.Controllers;

internal sealed class InspectionController
{
    private readonly IInspectionRecordService _inspectionRecordService;
    private readonly InspectionExcelExporter _excelExporter;

    public InspectionController(
        IInspectionRecordService inspectionRecordService,
        InspectionExcelExporter excelExporter)
    {
        _inspectionRecordService = inspectionRecordService;
        _excelExporter = excelExporter;
    }

    public InspectionDashboardViewModel Load(InspectionFilterViewModel filter)
    {
        var result = _inspectionRecordService.Query(ToQuery(filter));
        return new InspectionDashboardViewModel
        {
            LineOptions = result.LineOptions,
            Records = result.Records
                .Select(record => new InspectionRecordViewModel
                {
                    CheckedAt = record.CheckedAt.ToString("yyyy-MM-dd HH:mm"),
                    LineName = record.LineName,
                    DeviceName = record.DeviceName,
                    InspectionItem = record.InspectionItem,
                    Inspector = record.Inspector,
                    StatusText = record.Status.ToDisplayText(),
                    MeasuredValueText = record.MeasuredValue.ToString("0.##"),
                    Remark = record.Remark
                })
                .ToList(),
            TrendPoints = result.Summary.TrendPoints
                .Select(point => new InspectionTrendPointViewModel
                {
                    Label = point.Label,
                    NormalCount = point.NormalCount,
                    WarningCount = point.WarningCount,
                    AbnormalCount = point.AbnormalCount
                })
                .ToList(),
            TotalCount = result.Summary.TotalCount,
            NormalCount = result.Summary.NormalCount,
            WarningCount = result.Summary.WarningCount,
            AbnormalCount = result.Summary.AbnormalCount,
            PassRateText = $"{result.Summary.PassRate:0.0}%",
            GeneratedAt = result.GeneratedAt
        };
    }

    public void Add(InspectionEntryViewModel entry)
    {
        _inspectionRecordService.Add(new InspectionRecordDraft(
            entry.LineName,
            entry.DeviceName,
            entry.InspectionItem,
            entry.Inspector,
            entry.Status,
            entry.MeasuredValue,
            entry.CheckedAt,
            entry.Remark));
    }

    public void Export(string filePath, InspectionFilterViewModel filter)
    {
        var result = _inspectionRecordService.Query(ToQuery(filter));
        _excelExporter.Export(filePath, result);
    }

    private static InspectionQuery ToQuery(InspectionFilterViewModel filter)
    {
        return new InspectionQuery(
            filter.Keyword,
            filter.LineName,
            filter.Status,
            filter.StartTime,
            filter.EndTime);
    }
}
