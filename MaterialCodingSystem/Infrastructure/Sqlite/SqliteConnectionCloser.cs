using System.Threading;
using System.Threading.Tasks;
using MaterialCodingSystem.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Infrastructure.Sqlite;

public sealed class SqliteConnectionCloser : IDatabaseConnectionCloser
{
    private readonly SqliteConnection _connection;

    public SqliteConnectionCloser(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task CloseAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            if (_connection.State != System.Data.ConnectionState.Closed)
            {
                await _connection.CloseAsync();
            }
        }
        finally
        {
            // Ensure pooled connections release file handles before file replace.
            SqliteConnection.ClearPool(_connection);
            SqliteConnection.ClearAllPools();
            await _connection.DisposeAsync();
        }
    }
}

