using App.Core.Interfaces;
using App.Core.Models;

namespace App.Core.Services;

public sealed class InspectionRecordService : IInspectionRecordService
{
    private readonly IInspectionRecordRepository _repository;

    public InspectionRecordService(IInspectionRecordRepository repository)
    {
        _repository = repository;
        EnsureSeedData();
    }

    public InspectionQueryResult Query(InspectionQuery query)
    {
        var allRecords = _repository.GetAll();
        var filteredRecords = ApplyQuery(allRecords, query)
            .OrderByDescending(record => record.CheckedAt)
            .ToList();

        var lineOptions = allRecords
            .Select(record => record.LineName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new InspectionQueryResult(
            filteredRecords,
            lineOptions,
            BuildSummary(filteredRecords, query),
            DateTime.Now);
    }

    public InspectionRecord Add(InspectionRecordDraft draft)
    {
        var lineName = draft.LineName.Trim();
        var deviceName = draft.DeviceName.Trim();
        var inspectionItem = draft.InspectionItem.Trim();
        var inspector = draft.Inspector.Trim();

        if (string.IsNullOrWhiteSpace(lineName))
        {
            throw new InvalidOperationException("请输入产线名称。");
        }

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new InvalidOperationException("请输入设备名称。");
        }

        if (string.IsNullOrWhiteSpace(inspectionItem))
        {
            throw new InvalidOperationException("请输入点检项目。");
        }

        if (string.IsNullOrWhiteSpace(inspector))
        {
            throw new InvalidOperationException("请输入点检人。");
        }

        var record = new InspectionRecord(
            Guid.NewGuid(),
            lineName,
            deviceName,
            inspectionItem,
            inspector,
            draft.Status,
            Math.Round(draft.MeasuredValue, 2),
            draft.CheckedAt,
            draft.Remark.Trim());

        var allRecords = _repository.GetAll().ToList();
        allRecords.Add(record);
        _repository.SaveAll(allRecords);
        return record;
    }

    private void EnsureSeedData()
    {
        if (_repository.GetAll().Count > 0)
        {
            return;
        }

        _repository.SaveAll(CreateSeedRecords());
    }

    private static IReadOnlyList<InspectionRecord> ApplyQuery(
        IReadOnlyList<InspectionRecord> records,
        InspectionQuery query)
    {
        var keyword = query.Keyword.Trim();
        IEnumerable<InspectionRecord> filtered = records;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = filtered.Where(record =>
                record.LineName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                record.DeviceName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                record.InspectionItem.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                record.Inspector.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                record.Remark.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.LineName))
        {
            filtered = filtered.Where(record => string.Equals(
                record.LineName,
                query.LineName,
                StringComparison.OrdinalIgnoreCase));
        }

        if (query.Status.HasValue)
        {
            filtered = filtered.Where(record => record.Status == query.Status.Value);
        }

        if (query.StartTime.HasValue)
        {
            filtered = filtered.Where(record => record.CheckedAt >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            filtered = filtered.Where(record => record.CheckedAt <= query.EndTime.Value);
        }

        return filtered.ToList();
    }

    private static InspectionSummary BuildSummary(
        IReadOnlyList<InspectionRecord> records,
        InspectionQuery query)
    {
        var normalCount = records.Count(record => record.Status == InspectionStatus.Normal);
        var warningCount = records.Count(record => record.Status == InspectionStatus.Warning);
        var abnormalCount = records.Count(record => record.Status == InspectionStatus.Abnormal);
        var totalCount = records.Count;
        var passRate = totalCount == 0
            ? 0
            : Math.Round(normalCount * 100m / totalCount, 1);

        return new InspectionSummary(
            totalCount,
            normalCount,
            warningCount,
            abnormalCount,
            passRate,
            BuildTrendPoints(records, query));
    }

    private static IReadOnlyList<InspectionTrendPoint> BuildTrendPoints(
        IReadOnlyList<InspectionRecord> records,
        InspectionQuery query)
    {
        var end = query.EndTime?.AddMinutes(1) ?? DateTime.Now;
        var start = query.StartTime ?? end.AddHours(-7);
        var useDailyBuckets = (end - start).TotalDays > 2;

        if (useDailyBuckets)
        {
            var endDay = end.Date;
            return Enumerable.Range(0, 7)
                .Select(offset => endDay.AddDays(offset - 6))
                .Select(dayStart =>
                {
                    var dayEnd = dayStart.AddDays(1);
                    var dayRecords = records.Where(record =>
                        record.CheckedAt >= dayStart &&
                        record.CheckedAt < dayEnd)
                        .ToList();

                    return new InspectionTrendPoint(
                        dayStart.ToString("MM-dd"),
                        dayRecords.Count(record => record.Status == InspectionStatus.Normal),
                        dayRecords.Count(record => record.Status == InspectionStatus.Warning),
                        dayRecords.Count(record => record.Status == InspectionStatus.Abnormal));
                })
                .ToList();
        }

        var endHour = new DateTime(end.Year, end.Month, end.Day, end.Hour, 0, 0);
        return Enumerable.Range(0, 8)
            .Select(offset => endHour.AddHours(offset - 7))
            .Select(hourStart =>
            {
                var hourEnd = hourStart.AddHours(1);
                var hourRecords = records.Where(record =>
                    record.CheckedAt >= hourStart &&
                    record.CheckedAt < hourEnd)
                    .ToList();

                return new InspectionTrendPoint(
                    hourStart.ToString("HH:mm"),
                    hourRecords.Count(record => record.Status == InspectionStatus.Normal),
                    hourRecords.Count(record => record.Status == InspectionStatus.Warning),
                    hourRecords.Count(record => record.Status == InspectionStatus.Abnormal));
            })
            .ToList();
    }

    private static IReadOnlyList<InspectionRecord> CreateSeedRecords()
    {
        var now = DateTime.Now;
        return new List<InspectionRecord>
        {
            new(Guid.NewGuid(), "一线", "冲压机-A01", "液压压力", "张磊", InspectionStatus.Normal, 78.5m, now.AddHours(-6), "参数稳定"),
            new(Guid.NewGuid(), "一线", "冲压机-A01", "油温", "张磊", InspectionStatus.Warning, 92.1m, now.AddHours(-5), "接近阈值"),
            new(Guid.NewGuid(), "二线", "装配机-B03", "夹具磨损", "李敏", InspectionStatus.Normal, 12.0m, now.AddHours(-4), "无异常"),
            new(Guid.NewGuid(), "二线", "装配机-B03", "振动值", "李敏", InspectionStatus.Abnormal, 14.6m, now.AddHours(-3), "需安排停机检查"),
            new(Guid.NewGuid(), "三线", "包装机-C02", "封口温度", "王娜", InspectionStatus.Normal, 186.3m, now.AddHours(-2), "波动正常"),
            new(Guid.NewGuid(), "三线", "包装机-C02", "传送速度", "王娜", InspectionStatus.Warning, 63.2m, now.AddHours(-1), "速度偏慢"),
            new(Guid.NewGuid(), "一线", "空压机-AUX", "排气压力", "陈博", InspectionStatus.Normal, 8.2m, now.AddMinutes(-35), "巡检通过"),
            new(Guid.NewGuid(), "二线", "焊接臂-B07", "焊缝质量", "周宁", InspectionStatus.Abnormal, 54.0m, now.AddMinutes(-10), "抽检不合格")
        };
    }
}
