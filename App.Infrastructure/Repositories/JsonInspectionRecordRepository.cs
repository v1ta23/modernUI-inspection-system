using System.Text.Json;
using System.Text.Json.Serialization;
using App.Core.Interfaces;
using App.Core.Models;

namespace App.Infrastructure.Repositories;

public sealed class JsonInspectionRecordRepository : IInspectionRecordRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly object _syncRoot = new();

    public JsonInspectionRecordRepository(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<InspectionRecord> GetAll()
    {
        lock (_syncRoot)
        {
            if (!File.Exists(_filePath))
            {
                return Array.Empty<InspectionRecord>();
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<InspectionRecord>();
            }

            return JsonSerializer.Deserialize<List<InspectionRecord>>(json, SerializerOptions) ??
                   new List<InspectionRecord>();
        }
    }

    public void SaveAll(IReadOnlyList<InspectionRecord> records)
    {
        lock (_syncRoot)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(records, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
    }
}
