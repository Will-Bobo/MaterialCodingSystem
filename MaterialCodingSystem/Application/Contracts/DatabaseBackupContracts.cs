namespace MaterialCodingSystem.Application.Contracts;

public sealed record DatabaseExportResponse(string TargetPath);

public sealed record AutoBackupResponse(string BackupPath, int RetentionCount);

public sealed record DatabaseRestoreResponse(
    string MainDbPath,
    string BackupPath,
    bool RestartRequired
);

