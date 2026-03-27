using System.Text.Json;
using System.Text.Json.Serialization;

using MyOwnPo.Models;

namespace MyOwnPo.Services;

public class JsonContextFileStore(string filePath) : IContextFileStore
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _filePath = filePath;

    public ProjectContext? Load()
    {
        if (!File.Exists(_filePath))
            return null;

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<ProjectContext>(json, ReadOptions);
    }

    public void Save(ProjectContext context)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(context, WriteOptions);
        File.WriteAllText(_filePath, json);
    }

    public void Delete()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }
}