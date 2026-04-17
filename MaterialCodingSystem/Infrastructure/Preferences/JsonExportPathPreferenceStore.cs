using System.IO;
using System.Text.Json;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Infrastructure.Preferences;

public sealed class JsonExportPathPreferenceStore : IExportPathPreferenceStore, IBomArchivePreferenceStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonExportPathPreferenceStore(string filePath)
    {
        _filePath = filePath;
    }

    public string? GetLastExportDirectory()
    {
        var dto = Read();
        return string.IsNullOrWhiteSpace(dto?.LastExportDirectory) ? null : dto!.LastExportDirectory;
    }

    public void SetLastExportDirectory(string directory)
    {
        var dir = string.IsNullOrWhiteSpace(directory) ? null : directory.Trim();
        var dto = Read() ?? new Dto();
        dto.LastExportDirectory = dir;
        Write(dto);
    }

    public string? GetBomArchiveRootPath()
    {
        var dto = Read();
        return string.IsNullOrWhiteSpace(dto?.BomArchiveRootPath) ? null : dto!.BomArchiveRootPath;
    }

    public void SetBomArchiveRootPath(string rootPath)
    {
        var v = string.IsNullOrWhiteSpace(rootPath) ? null : rootPath.Trim();
        var dto = Read() ?? new Dto();
        dto.BomArchiveRootPath = v;
        Write(dto);
    }

    private Dto? Read()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Dto>(json);
        }
        catch
        {
            return null;
        }
    }

    private void Write(Dto dto)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(_filePath));
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(dto, JsonOptions));
    }

    private sealed class Dto
    {
        public string? LastExportDirectory { get; set; }
        public string? BomArchiveRootPath { get; set; }
    }
}
