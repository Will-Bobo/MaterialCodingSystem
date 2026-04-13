using MaterialCodingSystem.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Infrastructure.Sqlite;

public sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly SqliteConnection _connection;
    private const int BeginTxMaxAttempts = 5;

    public SqliteUnitOfWork(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(ct);
        }

        // Shared-cache in-memory sqlite concurrency: BeginTransaction may fail transiently under contention.
        // We retry starting the transaction a few times (no business logic here; just robustness).
        SqliteTransaction tx = await BeginTransactionWithRetryAsync(ct);
        AmbientSqliteContext.CurrentTransaction = tx;
        try
        {
            var result = await action();
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { /* ignore */ }
            throw;
        }
        finally
        {
            AmbientSqliteContext.CurrentTransaction = null;
        }
    }

    private async Task<SqliteTransaction> BeginTransactionWithRetryAsync(CancellationToken ct)
    {
        var delayMs = 10;
        for (var attempt = 1; attempt <= BeginTxMaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return _connection.BeginTransaction();
            }
            catch (SqliteException) when (attempt < BeginTxMaxAttempts)
            {
                await Task.Delay(delayMs, ct);
                delayMs = Math.Min(delayMs * 2, 200);
            }
        }

        // last attempt throws
        return _connection.BeginTransaction();
    }
}

