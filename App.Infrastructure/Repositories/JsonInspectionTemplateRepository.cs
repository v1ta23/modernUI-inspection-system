using System.Text.Json;
using App.Core.Interfaces;
using App.Core.Models;

namespace App.Infrastructure.Repositories;

public sealed class JsonInspectionTemplateRepository : IInspectionTemplateRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly object _syncRoot = new();

    public JsonInspectionTemplateRepository(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<InspectionTemplate> GetAll()
    {
        lock (_syncRoot)
        {
            if (!File.Exists(_filePath))
            {
                return Array.Empty<InspectionTemplate>();
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<InspectionTemplate>();
            }

            return JsonSerializer.Deserialize<List<InspectionTemplate>>(json, SerializerOptions) ??
                   new List<InspectionTemplate>();
        }
    }

    public void SaveAll(IReadOnlyList<InspectionTemplate> templates)
    {
        lock (_syncRoot)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(templates, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
    }
}
