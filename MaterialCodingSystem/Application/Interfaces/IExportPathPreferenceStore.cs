namespace MaterialCodingSystem.Application.Interfaces;

public interface IExportPathPreferenceStore
{
    string? GetLastExportDirectory();

    void SetLastExportDirectory(string directory);
}
