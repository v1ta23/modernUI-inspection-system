using App.Core.Models;

namespace App.Core.Interfaces;

public interface IInspectionRecordRepository
{
    IReadOnlyList<InspectionRecord> GetAll();

    void SaveAll(IReadOnlyList<InspectionRecord> records);
}
