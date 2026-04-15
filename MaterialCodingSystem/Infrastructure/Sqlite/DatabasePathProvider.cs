using System;
using System.IO;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Infrastructure.Sqlite;

public sealed class DatabasePathProvider : IDatabasePathProvider
{
    private const string AppFolderName = "MaterialCodingSystem";

    public string GetMainDbPath()
    {
        var baseDir = GetAppDataDirectorySafe();
        TryCreateDirectorySafe(baseDir);
        return Path.Combine(baseDir, "mcs.db");
    }

    public string GetBackupDirectory()
    {
        var baseDir = GetAppDataDirectorySafe();
        var backupDir = Path.Combine(baseDir, "backup");
        TryCreateDirectorySafe(backupDir);
        return backupDir;
    }

    private static string GetAppDataDirectorySafe()
    {
        try
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(dir))
                return Path.Combine(".", AppFolderName);
            return Path.Combine(dir, AppFolderName);
        }
        catch
        {
            return Path.Combine(".", AppFolderName);
        }
    }

    private static void TryCreateDirectorySafe(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path))
                Directory.CreateDirectory(path);
        }
        catch
        {
            // V1: do not crash on path provisioning. Downstream operations will surface errors via Result<T>.
        }
    }
}

