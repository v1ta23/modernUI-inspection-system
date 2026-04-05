using App.Core.Models;

namespace App.Core.Interfaces;

public interface IInspectionRecordService
{
    InspectionQueryResult Query(InspectionQuery query);

    InspectionRecord Add(InspectionRecordDraft draft);

    InspectionRecord Update(Guid id, InspectionRecordDraft draft);

    InspectionRecord Close(Guid id, string account, string closureRemark);

    InspectionRecord Revoke(Guid id, string account, string revokeReason);

    void Delete(Guid id);

    IReadOnlyList<InspectionTemplate> GetTemplates();

    InspectionTemplate SaveTemplate(InspectionTemplateDraft draft);

    void DeleteTemplate(Guid id);
}
