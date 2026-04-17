using System.Security.Cryptography;
using ClosedXML.Excel;
using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Infrastructure.Excel;
using MaterialCodingSystem.Infrastructure.Sqlite;
using MaterialCodingSystem.Infrastructure.Storage;
using MaterialCodingSystem.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class Phase2_BomArchiveAndImportTests
{
    [Fact]
    public async Task ImportNew_Success_Should_Refresh_To_PASS()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var conn = db.Connection;
        await conn.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','R');");

        var repo = new SqliteMaterialRepository(conn);
        var uow = new SqliteUnitOfWork(conn);
        var gridParser = new UnifiedBomGridParser(
            NullLogger<UnifiedBomGridParser>.Instance,
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter());
        var parseBom = new ParseBomUseCase(gridParser);
        var detector = new MaterialCodingSystem.Infrastructure.Excel.BomFileFormatDetector();
        var analyze = new AnalyzeBomUseCase(parseBom, detector, repo, uow, NullLogger<AnalyzeBomUseCase>.Instance);
        var app = new MaterialApplicationService(uow, repo);
        var import = new ImportBomNewMaterialsUseCase(analyze, app, new BomImportInProgressGate(), NullLogger<ImportBomNewMaterialsUseCase>.Instance);

        var path = WriteBomTempFile(wb =>
        {
            var ws = wb.AddWorksheet("BOM");
            ws.Cell(1, 1).Value = "成品编码";
            ws.Cell(1, 2).Value = "CP";
            ws.Cell(2, 1).Value = "PCBA版本号";
            ws.Cell(2, 2).Value = "KC001/V1:2026";

            ws.Cell(5, 1).Value = "编码";
            ws.Cell(5, 2).Value = "名称";
            ws.Cell(5, 3).Value = "描述";
            ws.Cell(5, 4).Value = "规格";
            ws.Cell(5, 5).Value = "品牌";

            ws.Cell(6, 1).Value = "ZDA0000009A";
            ws.Cell(6, 2).Value = "n9";
            ws.Cell(6, 3).Value = "d9";
            ws.Cell(6, 4).Value = "S-NEW";
            ws.Cell(6, 5).Value = "B";
        });

        try
        {
            var before = await analyze.ExecuteAsync(new AnalyzeBomRequest(path));
            Assert.True(before.IsSuccess);
            Assert.Equal(1, before.Data!.NewCount);

            var after = await import.ExecuteAsync(new ImportBomNewMaterialsRequest(path));
            Assert.True(after.IsSuccess, after.Error?.Message);
            Assert.Equal(1, after.Data!.AnalyzeResult.PassCount);
            Assert.Equal(0, after.Data.AnalyzeResult.NewCount);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportNew_When_Spec_Duplicate_Should_Return_ERROR_With_Reason()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var conn = db.Connection;
        await conn.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','R');");
        await conn.ExecuteAsync("INSERT INTO material_group(id,category_id,category_code,serial_no) VALUES (1,1,'ZDA',1);");
        await conn.ExecuteAsync(@"
INSERT INTO material_item(group_id,category_id,category_code,code,suffix,name,description,spec,spec_normalized,brand,status)
VALUES (1,1,'ZDA','ZDA0000001A','A','R','d1','S-DUP','D1',NULL,1);
");

        var repo = new SqliteMaterialRepository(conn);
        var uow = new SqliteUnitOfWork(conn);
        var gridParser = new UnifiedBomGridParser(
            NullLogger<UnifiedBomGridParser>.Instance,
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter());
        var parseBom = new ParseBomUseCase(gridParser);
        var detector = new MaterialCodingSystem.Infrastructure.Excel.BomFileFormatDetector();
        var analyze = new AnalyzeBomUseCase(parseBom, detector, repo, uow, NullLogger<AnalyzeBomUseCase>.Instance);
        var app = new MaterialApplicationService(uow, repo);
        var import = new ImportBomNewMaterialsUseCase(analyze, app, new BomImportInProgressGate(), NullLogger<ImportBomNewMaterialsUseCase>.Instance);

        var path = WriteBomTempFile(wb =>
        {
            var ws = wb.AddWorksheet("BOM");
            ws.Cell(1, 1).Value = "成品编码";
            ws.Cell(1, 2).Value = "CP";
            ws.Cell(2, 1).Value = "PCBA版本号";
            ws.Cell(2, 2).Value = "V";

            ws.Cell(5, 1).Value = "编码";
            ws.Cell(5, 2).Value = "名称";
            ws.Cell(5, 3).Value = "描述";
            ws.Cell(5, 4).Value = "规格";
            ws.Cell(5, 5).Value = "品牌";

            ws.Cell(6, 1).Value = "ZDA0000009A";
            ws.Cell(6, 2).Value = "n9";
            ws.Cell(6, 3).Value = "d9";
            ws.Cell(6, 4).Value = "S-DUP";
            ws.Cell(6, 5).Value = "";
        });

        try
        {
            var after = await import.ExecuteAsync(new ImportBomNewMaterialsRequest(path));
            Assert.True(after.IsSuccess, after.Error?.Message);
            Assert.Equal(1, after.Data!.AnalyzeResult.ErrorCount);
            Assert.Equal("规格已存在，疑似重复建料", after.Data.AnalyzeResult.Rows[0].ErrorReason);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Archive_When_Duplicate_Version_Should_Be_BOM_ARCHIVE_VERSION_EXISTS()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var bomRepo = new SqliteBomArchiveRepository(db.Connection);
        var storage = new FileSystemBomArchiveStorage();
        var dirs = new TestExecDirProvider();
        var svc = new BomArchiveService(bomRepo, storage, dirs, NullLogger<BomArchiveService>.Instance);

        var src = WriteDummyFile();
        try
        {
            var first = await svc.ArchiveAsync(src, "CP00A1062A", "KC001/V1:2026");
            Assert.True(first.IsSuccess, first.Error?.Message);

            var second = await svc.ArchiveAsync(src, "CP00A1062A", "KC001/V1:2026");
            Assert.False(second.IsSuccess);
            Assert.Equal(ErrorCodes.BOM_ARCHIVE_VERSION_EXISTS, second.Error!.Code);
        }
        finally
        {
            SafeDelete(src);
            dirs.Cleanup();
        }
    }

    [Fact]
    public async Task Archive_Should_Copy_Original_File_Bytes_Exactly()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var bomRepo = new SqliteBomArchiveRepository(db.Connection);
        var storage = new FileSystemBomArchiveStorage();
        var dirs = new TestExecDirProvider();
        var svc = new BomArchiveService(bomRepo, storage, dirs, NullLogger<BomArchiveService>.Instance);

        var src = WriteDummyFile();
        try
        {
            var srcHash = Sha256(src);
            var res = await svc.ArchiveAsync(src, "CP", "KC001/V1:2026");
            Assert.True(res.IsSuccess, res.Error?.Message);
            var dst = res.Data!;
            Assert.True(File.Exists(dst));
            Assert.Equal(srcHash, Sha256(dst));
        }
        finally
        {
            SafeDelete(src);
            dirs.Cleanup();
        }
    }

    [Fact]
    public async Task Archive_When_Source_File_Locked_Should_Return_BOM_FILE_LOCKED()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var bomRepo = new SqliteBomArchiveRepository(db.Connection);
        var storage = new FileSystemBomArchiveStorage();
        var dirs = new TestExecDirProvider();
        var svc = new BomArchiveService(bomRepo, storage, dirs, NullLogger<BomArchiveService>.Instance);

        var src = WriteDummyFile();
        await using var lockHandle = new FileStream(src, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var res = await svc.ArchiveAsync(src, "CP", "V");
            Assert.False(res.IsSuccess);
            Assert.Equal(ErrorCodes.BOM_FILE_LOCKED, res.Error!.Code);
        }
        finally
        {
            lockHandle.Dispose();
            SafeDelete(src);
            dirs.Cleanup();
        }
    }

    private static string WriteBomTempFile(Action<XLWorkbook> build)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcs_bom_{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();
        build(wb);
        wb.SaveAs(path);
        return path;
    }

    private static string WriteDummyFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcs_src_{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(path, "dummy");
        return path;
    }

    private static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        var bytes = File.ReadAllBytes(path);
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private static void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private sealed class TestExecDirProvider : IAppExecutionDirectoryProvider
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "mcs_exec_" + Guid.NewGuid().ToString("N"));
        public string GetExecutionDirectory() => _dir;
        public void Cleanup()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }
    }
}

