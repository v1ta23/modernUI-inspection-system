using App.Core.Models;

namespace App.Core.Interfaces;

public interface IInspectionRecordService
{
    InspectionQueryResult Query(InspectionQuery query);

    InspectionRecord Add(InspectionRecordDraft draft);
}
