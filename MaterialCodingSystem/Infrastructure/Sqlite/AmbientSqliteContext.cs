using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Infrastructure.Sqlite;

internal static class AmbientSqliteContext
{
    private static readonly AsyncLocal<SqliteTransaction?> _tx = new();

    public static SqliteTransaction? CurrentTransaction
    {
        get => _tx.Value;
        set => _tx.Value = value;
    }
}

