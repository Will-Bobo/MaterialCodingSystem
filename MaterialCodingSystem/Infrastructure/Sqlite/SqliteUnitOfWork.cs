using MaterialCodingSystem.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Infrastructure.Sqlite;

public sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly SqliteConnection _connection;

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

        await using var tx = _connection.BeginTransaction();
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
}

