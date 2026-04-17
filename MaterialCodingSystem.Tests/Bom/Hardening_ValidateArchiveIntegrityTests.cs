using MaterialCodingSystem.Application;
using MaterialCodingSystem.Infrastructure.Sqlite;
using MaterialCodingSystem.Infrastructure.Storage;
using MaterialCodingSystem.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class Hardening_ValidateArchiveIntegrityTests
{
    [Fact]
    public async Task ValidateIntegrity_When_File_Missing_Should_Return_IsOk_False()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var conn = db.Connection;
        var repo = new SqliteBomArchiveRepository(conn);

        // insert record pointing to missing file
        await repo.InsertAsync("CP", "V", Path.Combine(Path.GetTempPath(), "no_such_file.xlsx"));

        var use = new ValidateBomArchiveIntegrityUseCase(repo);
        var src = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(src, "dummy");
            var res = await use.ExecuteAsync(new ValidateBomArchiveIntegrityRequest("CP", "V", src));
            Assert.True(res.IsSuccess);
            Assert.False(res.Data!.IsOk);
            Assert.Contains("不存在", res.Data.Reason);
        }
        finally
        {
            if (File.Exists(src)) File.Delete(src);
        }
    }
}

