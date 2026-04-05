using App.Core.Models;

namespace App.Core.Interfaces;

public interface IInspectionTemplateRepository
{
    IReadOnlyList<InspectionTemplate> GetAll();

    void SaveAll(IReadOnlyList<InspectionTemplate> templates);
}
