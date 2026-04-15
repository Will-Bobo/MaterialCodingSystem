using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MaterialCodingSystem.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Infrastructure.Sqlite;

public sealed class SqliteBackupRepository : IBackupRepository
{
    private readonly SqliteConnection _connection;
    private readonly IDatabasePathProvider _paths;

    public SqliteBackupRepository(SqliteConnection connection, IDatabasePathProvider paths)
    {
        _connection = connection;
        _paths = paths;
    }

    public async Task VacuumIntoAsync(string targetPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("targetPath is required.", nameof(targetPath));

        var full = Path.GetFullPath(targetPath.Trim());
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        // VACUUM INTO requires a string literal path. Must escape single quotes to avoid breaking SQL.
        var escaped = EscapeSqliteStringLiteral(full);
        var sql = $"VACUUM INTO '{escaped}';";

        try
        {
            if (_connection.State != System.Data.ConnectionState.Open)
                await _connection.OpenAsync(ct);

            await _connection.ExecuteAsync(new CommandDefinition(sql, transaction: AmbientSqliteContext.CurrentTransaction, cancellationToken: ct));
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException($"SQLite VACUUM INTO failed: {ex.SqliteErrorCode} {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Write backup file failed: {ex.Message}", ex);
        }
    }

    public Task<List<string>> ListBackupFilesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var dir = _paths.GetBackupDirectory();
        if (!Directory.Exists(dir))
            return Task.FromResult(new List<string>());

        var files = Directory.EnumerateFiles(dir, "mcs_*.db", SearchOption.TopDirectoryOnly)
            .OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult(files);
    }

    public Task DeleteFileAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path))
            return Task.CompletedTask;
        File.Delete(path);
        return Task.CompletedTask;
    }

    internal static string EscapeSqliteStringLiteral(string value) => value.Replace("'", "''");
}

