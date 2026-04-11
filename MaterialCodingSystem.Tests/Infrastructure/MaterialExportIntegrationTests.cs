using ClosedXML.Excel;
using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Infrastructure.Excel;
using MaterialCodingSystem.Infrastructure.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

public sealed class MaterialExportIntegrationTests
{
    [Fact]
    public async Task ListActiveItemsForExportAsync_Orders_By_Category_Serial_Suffix_And_Excludes_Deprecated()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var conn = db.Connection;

        await conn.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','R'),('ZDB','C');");
        await conn.ExecuteAsync(
            "INSERT INTO material_group(id,category_id,category_code,serial_no) VALUES (1,1,'ZDA',1),(2,2,'ZDB',1);");
        await conn.ExecuteAsync(@"
INSERT INTO material_item(group_id,category_id,category_code,code,suffix,name,description,spec,spec_normalized,brand,status)
VALUES
 (1,1,'ZDA','ZDA0000001A','A','n1','d1','S1','D1',NULL,1),
 (1,1,'ZDA','ZDA0000001B','B','n2','d2','S2','D2',NULL,0),
 (2,2,'ZDB','ZDB0000001A','A','n3','d3','S3','D3','B3',1);
");

        var repo = new SqliteMaterialRepository(conn);
        var rows = await repo.ListActiveItemsForExportAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal("ZDA0000001A", rows[0].Code);
        Assert.Equal("ZDB0000001A", rows[1].Code);
    }

    [Fact]
    public async Task ExportActiveMaterials_Writes_Excel_With_Sheet_Per_Category()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var conn = db.Connection;

        await conn.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','R'),('ZDB','C');");
        await conn.ExecuteAsync(
            "INSERT INTO material_group(id,category_id,category_code,serial_no) VALUES (1,1,'ZDA',1),(2,2,'ZDB',1);");
        await conn.ExecuteAsync(@"
INSERT INTO material_item(group_id,category_id,category_code,code,suffix,name,description,spec,spec_normalized,brand,status)
VALUES
 (1,1,'ZDA','ZDA0000001A','A','n1','d1','S1','D1',NULL,1),
 (2,2,'ZDB','ZDB0000001A','A','n3','d3','S3','D3','B3',1);
");

        var repo = new SqliteMaterialRepository(conn);
        var uow = new SqliteUnitOfWork(conn);
        var exporter = new ClosedXmlMaterialExcelExporter();
        var app = new MaterialApplicationService(uow, repo, exporter);

        var path = Path.Combine(Path.GetTempPath(), $"mcs_export_{Guid.NewGuid():N}.xlsx");
        try
        {
            var res = await app.ExportActiveMaterials(path);
            Assert.True(res.IsSuccess, res.Error?.Message);
            using var wb = new XLWorkbook(path);
            Assert.Equal(2, wb.Worksheets.Count);
            var zda = wb.Worksheet("ZDA");
            Assert.Equal("ZDA0000001A", zda.Cell(2, 1).GetString());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
