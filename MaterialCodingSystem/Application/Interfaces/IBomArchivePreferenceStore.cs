namespace MaterialCodingSystem.Application.Interfaces;

public interface IBomArchivePreferenceStore
{
    string? GetBomArchiveRootPath();

    void SetBomArchiveRootPath(string rootPath);
}

