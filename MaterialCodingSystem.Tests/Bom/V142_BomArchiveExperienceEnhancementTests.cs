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
using Xunit;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class V142_BomArchiveExperienceEnhancementTests
{
    [Fact]
    public async Task Archive_FirstTime_NoRootPath_Should_PromptPickFolder_AndPersistPreference()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var (useCase, prefs, ui, bomPath, root) = await CreateArchiveUseCaseAsync(db);
        prefs.RootPath = null;
        ui.NextPickedFolder = root;

        var res = await useCase.ExecuteAsync(new ArchiveBomRequest(bomPath));

        Assert.True(res.IsSuccess, res.Error?.Message);
        Assert.Equal(root, prefs.RootPath);
        Assert.True(ui.PickCalled);
        Assert.True(File.Exists(res.Data!.FilePath));
    }

    [Fact]
    public async Task Archive_Success_Should_ShowSavedPrompt()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var (useCase, prefs, ui, bomPath, root) = await CreateArchiveUseCaseAsync(db);
        prefs.RootPath = root;

        var res = await useCase.ExecuteAsync(new ArchiveBomRequest(bomPath));

        Assert.True(res.IsSuccess, res.Error?.Message);
        Assert.True(ui.SavedPromptCalled);
    }

    [Fact]
    public async Task Archive_DuplicateVersion_Should_ConfirmOverwrite()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var (useCase, prefs, ui, bomPath, root) = await CreateArchiveUseCaseAsync(db);
        prefs.RootPath = root;

        var first = await useCase.ExecuteAsync(new ArchiveBomRequest(bomPath));
        Assert.True(first.IsSuccess);

        ui.NextConfirmOverwrite = true;
        var second = await useCase.ExecuteAsync(new ArchiveBomRequest(bomPath));

        Assert.True(ui.OverwriteConfirmCalled);
        Assert.True(second.IsSuccess, second.Error?.Message);
    }

    [Fact]
    public async Task Archive_Overwrite_Should_UpdateCreatedAt()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var (useCase, prefs, ui, bomPath, root) = await CreateArchiveUseCaseAsync(db);
        prefs.RootPath = root;

        var first = await useCase.ExecuteAsync(new ArchiveBomRequest(bomPath));
        Assert.True(first.IsSuccess);

        // ensure CURRENT_TIMESTAMP changes (1s granularity)
        await Task.Delay(1100);

        ui.NextConfirmOverwrite = true;
        var second = await useCase.ExecuteAsync(new ArchiveBomRequest(bomPath));
        Assert.True(second.IsSuccess);

        var repo = new SqliteBomArchiveRepository(db.Connection);
        var record = await repo.GetAsync("CP", "V1");
        Assert.NotNull(record);
        Assert.NotEqual(ui.FirstCreatedAt, record!.CreatedAt);
    }

    [Fact]
    public async Task Archive_Overwrite_WhenTargetLocked_Should_Return_BOM_FILE_LOCKED_And_NoTmpLeft()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var (useCase, prefs, ui, bomPath, root) = await CreateArchiveUseCaseAsync(db);
        prefs.RootPath = root;

        var first = await useCase.ExecuteAsync(new ArchiveBomRequest(bomPath));
        Assert.True(first.IsSuccess);

        ui.NextConfirmOverwrite = true;
        var target = first.Data!.FilePath;
        var tmp = target + ".tmp";

        await using var lockHandle = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var second = await useCase.ExecuteAsync(new ArchiveBomRequest(bomPath));

        Assert.False(second.IsSuccess);
        Assert.Equal(ErrorCodes.BOM_FILE_LOCKED, second.Error!.Code);
        Assert.False(File.Exists(tmp));
    }

    [Fact]
    public async Task Archive_Overwrite_WhenUserCancels_Should_NotChangeRecord()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var (useCase, prefs, ui, bomPath, root) = await CreateArchiveUseCaseAsync(db);
        prefs.RootPath = root;

        var first = await useCase.ExecuteAsync(new ArchiveBomRequest(bomPath));
        Assert.True(first.IsSuccess);

        var repo = new SqliteBomArchiveRepository(db.Connection);
        var before = await repo.GetAsync("CP", "V1");
        Assert.NotNull(before);

        ui.NextConfirmOverwrite = false;
        var second = await useCase.ExecuteAsync(new ArchiveBomRequest(bomPath));

        Assert.False(second.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, second.Error!.Code);

        var after = await repo.GetAsync("CP", "V1");
        Assert.NotNull(after);
        Assert.Equal(before!.CreatedAt, after!.CreatedAt);
        Assert.Equal(before.FilePath, after.FilePath);
    }

    private sealed class FakeBomArchivePrefs : IBomArchivePreferenceStore
    {
        public string? RootPath { get; set; }
        public string? GetBomArchiveRootPath() => RootPath;
        public void SetBomArchiveRootPath(string rootPath) => RootPath = rootPath;
    }

    private sealed class FakeBomArchiveUi : IBomArchiveInteraction
    {
        public bool PickCalled { get; private set; }
        public bool OverwriteConfirmCalled { get; private set; }
        public bool SavedPromptCalled { get; private set; }
        public string? NextPickedFolder { get; set; }
        public bool NextConfirmOverwrite { get; set; }
        public bool NextAskOpenFolder { get; set; }

        public string? FirstCreatedAt { get; set; }

        public string? PickArchiveRootFolder(string? initialDirectory)
        {
            PickCalled = true;
            return NextPickedFolder;
        }

        public bool ConfirmOverwrite(BomArchiveOverwritePrompt prompt)
        {
            OverwriteConfirmCalled = true;
            FirstCreatedAt ??= prompt.ExistingCreatedAt;
            return NextConfirmOverwrite;
        }

        public bool ShowSavedAndAskOpenFolder(BomArchiveSavedPrompt prompt)
        {
            SavedPromptCalled = true;
            return NextAskOpenFolder;
        }

        public void OpenFolder(string folderPath)
        {
        }
    }

    private static async Task<(ArchiveBomUseCase useCase, FakeBomArchivePrefs prefs, FakeBomArchiveUi ui, string bomPath, string root)>
        CreateArchiveUseCaseAsync(SqliteTestDb db)
    {
        var conn = db.Connection;
        await conn.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','R');");
        await conn.ExecuteAsync("INSERT INTO material_group(id,category_id,category_code,serial_no) VALUES (1,1,'ZDA',1);");
        await conn.ExecuteAsync(@"
INSERT INTO material_item(group_id,category_id,category_code,code,suffix,name,display_name,description,spec,spec_normalized,brand,status)
VALUES (1,1,'ZDA','ZDA0000001A','A','R',NULL,'d','S-PASS','S-PASS',NULL,1);
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
        var bomRepo = new SqliteBomArchiveRepository(conn);
        var canArchive = new CanArchiveBomUseCase(analyze, bomRepo);

        var storage = new FileSystemBomArchiveStorage();
        var dirs = new TestExecDirProvider(); // unused when archiveRootPath provided
        var archiveSvc = new BomArchiveService(bomRepo, storage, dirs, NullLogger<BomArchiveService>.Instance);

        var prefs = new FakeBomArchivePrefs();
        var ui = new FakeBomArchiveUi();
        var useCase = new ArchiveBomUseCase(canArchive, archiveSvc, bomRepo, prefs, ui);

        var root = Path.Combine(Path.GetTempPath(), $"mcs_bom_root_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var bomPath = WriteBomXlsx();
        return (useCase, prefs, ui, bomPath, root);
    }

    private static string WriteBomXlsx()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcs_bom_pass_{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("BOM");
        ws.Cell(1, 1).Value = "成品编码";
        ws.Cell(1, 2).Value = "CP";
        ws.Cell(2, 1).Value = "PCBA版本号";
        ws.Cell(2, 2).Value = "V1";

        ws.Cell(5, 1).Value = "编码";
        ws.Cell(5, 2).Value = "名称";
        ws.Cell(5, 3).Value = "描述";
        ws.Cell(5, 4).Value = "规格";
        ws.Cell(5, 5).Value = "品牌";

        ws.Cell(6, 1).Value = "ZDA0000001A";
        ws.Cell(6, 2).Value = "n";
        ws.Cell(6, 3).Value = "d";
        ws.Cell(6, 4).Value = "S-PASS";
        ws.Cell(6, 5).Value = "";

        wb.SaveAs(path);
        return path;
    }

    private sealed class TestExecDirProvider : IAppExecutionDirectoryProvider
    {
        public string GetExecutionDirectory() => Path.Combine(Path.GetTempPath(), "mcs_exec_" + Guid.NewGuid().ToString("N"));
    }
}

