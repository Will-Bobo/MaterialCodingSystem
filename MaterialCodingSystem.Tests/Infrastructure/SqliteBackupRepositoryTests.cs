using MaterialCodingSystem.Infrastructure.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

public sealed class SqliteBackupRepositoryTests
{
    [Fact]
    public void EscapeSqliteStringLiteral_DoublesSingleQuotes()
    {
        var input = @"C:\x\ab'cd\db.db";
        var escaped = SqliteBackupRepository.EscapeSqliteStringLiteral(input);
        Assert.Equal(@"C:\x\ab''cd\db.db", escaped);
    }
}

