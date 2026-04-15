using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialCodingSystem.Infrastructure.Sqlite;

/// <summary>
/// Registers SQLite persistence (connection lifecycle and schema) for the composition root.
/// </summary>
public static class PersistenceModule
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string dbPath)
    {
        services.AddSingleton(_ =>
        {
            // Pooling must be disabled to ensure Restore can release file handles deterministically.
            var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False");
            conn.Open();
            SqliteSchema.EnsureCreated(conn);
            return conn;
        });

        return services;
    }
}
