using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Application.Logging;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Infrastructure.Sqlite;

public sealed class SqliteBomArchiveRepository : IBomArchiveRepository
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<SqliteBomArchiveRepository> _logger;

    public SqliteBomArchiveRepository(SqliteConnection connection, ILogger<SqliteBomArchiveRepository>? logger = null)
    {
        _connection = connection;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SqliteBomArchiveRepository>.Instance;
    }

    public Task<bool> ExistsAsync(string finishedCode, string version, CancellationToken ct = default)
        => _connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    """
                    SELECT 1
                    FROM bom_archive
                    WHERE finished_code = @finishedCode AND version = @version
                    LIMIT 1;
                    """,
                    new { finishedCode, version },
                    cancellationToken: ct))
            .ContinueWith(t => t.Result is not null, ct);

    public async Task InsertAsync(string finishedCode, string version, string filePath, CancellationToken ct = default)
    {
        var sql = """
                  INSERT INTO bom_archive(finished_code, version, file_path)
                  VALUES (@finishedCode, @version, @filePath);
                  """;
        try
        {
            await _connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { finishedCode, version, filePath },
                cancellationToken: ct));
        }
        catch (Exception ex)
        {
            McsLoggingExtensions.LogException(_logger, ex, McsActions.BomArchiveService, ErrorCodes.INTERNAL_ERROR);
            throw;
        }
    }

    public async Task UpdateAsync(string finishedCode, string version, string filePath, CancellationToken ct = default)
    {
        var sql = """
                  UPDATE bom_archive
                  SET file_path = @filePath,
                      created_at = CURRENT_TIMESTAMP
                  WHERE finished_code = @finishedCode
                    AND version = @version;
                  """;
        try
        {
            await _connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { finishedCode, version, filePath },
                cancellationToken: ct));
        }
        catch (Exception ex)
        {
            McsLoggingExtensions.LogException(_logger, ex, McsActions.BomArchiveService, ErrorCodes.INTERNAL_ERROR);
            throw;
        }
    }

    public async Task<BomArchiveRecord?> GetAsync(string finishedCode, string version, CancellationToken ct = default)
    {
        var sql = """
                  SELECT
                    id AS Id,
                    finished_code AS FinishedCode,
                    version AS Version,
                    file_path AS FilePath,
                    created_at AS CreatedAt
                  FROM bom_archive
                  WHERE finished_code = @finishedCode AND version = @version
                  LIMIT 1;
                  """;
        return await _connection.QuerySingleOrDefaultAsync<BomArchiveRecord>(new CommandDefinition(
            sql,
            new { finishedCode, version },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<BomArchiveRecord>> ListAsync(string? finishedCode = null, CancellationToken ct = default)
    {
        var where = string.IsNullOrWhiteSpace(finishedCode) ? "" : "WHERE finished_code = @finishedCode";
        var sql = $"""
                   SELECT
                     id AS Id,
                     finished_code AS FinishedCode,
                     version AS Version,
                     file_path AS FilePath,
                     created_at AS CreatedAt
                   FROM bom_archive
                   {where}
                   ORDER BY created_at DESC, id DESC;
                   """;

        var rows = await _connection.QueryAsync<BomArchiveRecord>(new CommandDefinition(
            sql,
            new { finishedCode },
            cancellationToken: ct));
        return rows.ToList();
    }
}

