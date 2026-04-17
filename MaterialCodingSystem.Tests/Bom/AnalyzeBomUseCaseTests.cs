using ClosedXML.Excel;
using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Excel;
using MaterialCodingSystem.Infrastructure.Sqlite;
using MaterialCodingSystem.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class AnalyzeBomUseCaseTests
{
    [Fact]
    public async Task Analyze_When_Code_Empty_Should_Be_ERROR_MissingCode_And_FirstErrorRowNo()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var repo = new SqliteMaterialRepository(db.Connection);
        var uow = new SqliteUnitOfWork(db.Connection);
        var gridParser = new UnifiedBomGridParser(
            NullLogger<UnifiedBomGridParser>.Instance,
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter());
        var parseBom = new ParseBomUseCase(gridParser);
        var detector = new MaterialCodingSystem.Infrastructure.Excel.BomFileFormatDetector();
        var useCase = new AnalyzeBomUseCase(parseBom, detector, repo, uow, NullLogger<AnalyzeBomUseCase>.Instance);

        var path = WriteBomTempFile(wb =>
        {
            var ws = wb.AddWorksheet("BOM");
            ws.Cell(1, 1).Value = "成品编码";
            ws.Cell(1, 2).Value = "CP00A1062A";
            ws.Cell(2, 1).Value = "PCBA版本号";
            ws.Cell(2, 2).Value = "V1.0-20260414";

            ws.Cell(5, 1).Value = "编码";
            ws.Cell(5, 2).Value = "名称";
            ws.Cell(5, 3).Value = "描述";
            ws.Cell(5, 4).Value = "规格";
            ws.Cell(5, 5).Value = "品牌";

            // excel row 6
            ws.Cell(6, 1).Value = "";
            ws.Cell(6, 2).Value = "n1";
            ws.Cell(6, 3).Value = "d1";
            ws.Cell(6, 4).Value = "S1";
            ws.Cell(6, 5).Value = "B1";
        });

        try
        {
            var res = await useCase.ExecuteAsync(new AnalyzeBomRequest(path));
            Assert.True(res.IsSuccess, res.Error?.Message);
            var v = res.Data!;
            Assert.Equal(1, v.TotalCount);
            Assert.Equal(0, v.PassCount);
            Assert.Equal(0, v.NewCount);
            Assert.Equal(1, v.ErrorCount);
            Assert.Equal(1, v.MissingCodeErrorCount);
            Assert.Equal(6, v.FirstErrorRowNo);
            Assert.Equal(BomAuditStatus.ERROR, v.Rows[0].Status);
            Assert.Equal("缺少物料编码", v.Rows[0].ErrorReason);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Analyze_When_CodeSpec_Match_Should_Be_PASS()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var conn = db.Connection;
        await conn.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','R');");
        await conn.ExecuteAsync("INSERT INTO material_group(id,category_id,category_code,serial_no) VALUES (1,1,'ZDA',1);");
        await conn.ExecuteAsync(@"
INSERT INTO material_item(group_id,category_id,category_code,code,suffix,name,description,spec,spec_normalized,brand,status)
VALUES (1,1,'ZDA','ZDA0000001A','A','R','d1','S-PASS','D1',NULL,1);
");

        var repo = new SqliteMaterialRepository(conn);
        var uow = new SqliteUnitOfWork(conn);
        var gridParser = new UnifiedBomGridParser(
            NullLogger<UnifiedBomGridParser>.Instance,
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter());
        var parseBom = new ParseBomUseCase(gridParser);
        var detector = new MaterialCodingSystem.Infrastructure.Excel.BomFileFormatDetector();
        var useCase = new AnalyzeBomUseCase(parseBom, detector, repo, uow, NullLogger<AnalyzeBomUseCase>.Instance);

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

            ws.Cell(6, 1).Value = "ZDA0000001A";
            ws.Cell(6, 2).Value = "n1";
            ws.Cell(6, 3).Value = "d1";
            ws.Cell(6, 4).Value = "S-PASS";
            ws.Cell(6, 5).Value = "";
        });

        try
        {
            var res = await useCase.ExecuteAsync(new AnalyzeBomRequest(path));
            Assert.True(res.IsSuccess, res.Error?.Message);
            Assert.Single(res.Data!.Rows);
            Assert.Equal(BomAuditStatus.PASS, res.Data.Rows[0].Status);
            Assert.Equal(1, res.Data.PassCount);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Analyze_When_Code_NotExists_And_Spec_Exists_Only_Deprecated_Should_Be_NEW()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var conn = db.Connection;
        await conn.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','R');");
        await conn.ExecuteAsync("INSERT INTO material_group(id,category_id,category_code,serial_no) VALUES (1,1,'ZDA',1);");
        // spec exists but deprecated => should NOT block NEW (SpecExistsAsync checks status=1 only)
        await conn.ExecuteAsync(@"
INSERT INTO material_item(group_id,category_id,category_code,code,suffix,name,description,spec,spec_normalized,brand,status)
VALUES (1,1,'ZDA','ZDA0000001A','A','R','d1','S-OLD','D1',NULL,0);
");

        var repo = new SqliteMaterialRepository(conn);
        var uow = new SqliteUnitOfWork(conn);
        var gridParser = new UnifiedBomGridParser(
            NullLogger<UnifiedBomGridParser>.Instance,
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter());
        var parseBom = new ParseBomUseCase(gridParser);
        var detector = new MaterialCodingSystem.Infrastructure.Excel.BomFileFormatDetector();
        var useCase = new AnalyzeBomUseCase(parseBom, detector, repo, uow, NullLogger<AnalyzeBomUseCase>.Instance);

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
            ws.Cell(6, 4).Value = "S-OLD";
            ws.Cell(6, 5).Value = "";
        });

        try
        {
            var res = await useCase.ExecuteAsync(new AnalyzeBomRequest(path));
            Assert.True(res.IsSuccess, res.Error?.Message);
            Assert.Single(res.Data!.Rows);
            Assert.Equal(BomAuditStatus.NEW, res.Data.Rows[0].Status);
            Assert.Equal(1, res.Data.NewCount);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Analyze_When_Missing_Required_Columns_Should_Fail_BOM_FILE_INVALID()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var repo = new SqliteMaterialRepository(db.Connection);
        var uow = new SqliteUnitOfWork(db.Connection);
        var gridParser = new UnifiedBomGridParser(
            NullLogger<UnifiedBomGridParser>.Instance,
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter());
        var parseBom = new ParseBomUseCase(gridParser);
        var detector = new MaterialCodingSystem.Infrastructure.Excel.BomFileFormatDetector();
        var useCase = new AnalyzeBomUseCase(parseBom, detector, repo, uow, NullLogger<AnalyzeBomUseCase>.Instance);

        var path = WriteBomTempFile(wb =>
        {
            var ws = wb.AddWorksheet("BOM");
            ws.Cell(1, 1).Value = "成品编码";
            ws.Cell(1, 2).Value = "CP";
            ws.Cell(2, 1).Value = "PCBA版本号";
            ws.Cell(2, 2).Value = "V";

            // missing "规格"
            ws.Cell(5, 1).Value = "编码";
            ws.Cell(5, 2).Value = "名称";
            ws.Cell(5, 3).Value = "描述";
            ws.Cell(5, 4).Value = "品牌";
        });

        try
        {
            var res = await useCase.ExecuteAsync(new AnalyzeBomRequest(path));
            Assert.False(res.IsSuccess);
            Assert.Equal(ErrorCodes.BOM_FILE_INVALID, res.Error!.Code);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
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
}

