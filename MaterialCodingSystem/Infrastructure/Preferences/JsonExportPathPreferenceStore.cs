using System.IO;
using System.Text.Json;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Infrastructure.Preferences;

public sealed class JsonExportPathPreferenceStore : IExportPathPreferenceStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonExportPathPreferenceStore(string filePath)
    {
        _filePath = filePath;
    }

    public string? GetLastExportDirectory()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            var json = File.ReadAllText(_filePath);
            var dto = JsonSerializer.Deserialize<Dto>(json);
            return string.IsNullOrWhiteSpace(dto?.LastExportDirectory) ? null : dto.LastExportDirectory;
        }
        catch
        {
            return null;
        }
    }

    public void SetLastExportDirectory(string directory)
    {
        var dir = string.IsNullOrWhiteSpace(directory) ? null : directory.Trim();
        var dto = new Dto { LastExportDirectory = dir };
        var folder = Path.GetDirectoryName(Path.GetFullPath(_filePath));
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(dto, JsonOptions));
    }

    private sealed class Dto
    {
        public string? LastExportDirectory { get; set; }
    }
}
