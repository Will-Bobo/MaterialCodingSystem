namespace MaterialCodingSystem.Application.Interfaces;

public interface IDatabasePathProvider
{
    string GetMainDbPath();
    string GetBackupDirectory();
}

