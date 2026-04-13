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
    public async Task ListAllItemsForExportAsync_Orders_By_Status_Category_Serial_Suffix_Code_And_Uses_CategoryName()
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
        var rows = await repo.ListAllItemsForExportAsync();

        Assert.Equal(3, rows.Count);
        // status DESC, category_code, serial_no, suffix, code
        Assert.Equal("ZDA0000001A", rows[0].Code);
        Assert.Equal(1, rows[0].Status);
        Assert.Equal("R", rows[0].Name); // name must come from category.name
        Assert.Equal("ZDB0000001A", rows[1].Code);
        Assert.Equal(1, rows[1].Status);
        Assert.Equal("C", rows[1].Name);
        Assert.Equal("ZDA0000001B", rows[2].Code);
        Assert.Equal(0, rows[2].Status);
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
 (1,1,'ZDA','ZDA0000001B','B','n2','d2','S2','D2',NULL,0),
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
            // Sheet1 + 2 category sheets
            Assert.Equal(3, wb.Worksheets.Count);
            Assert.NotNull(wb.Worksheet("全量"));
            var zda = wb.Worksheet("ZDA");
            // columns: code, category_code, name, spec, description, brand, status
            Assert.Equal("ZDA0000001A", zda.Cell(2, 1).GetString());
            Assert.Equal("ZDA", zda.Cell(2, 2).GetString());
            Assert.Equal("R", zda.Cell(2, 3).GetString()); // name from category
            Assert.Equal("S1", zda.Cell(2, 4).GetString());
            Assert.Equal("d1", zda.Cell(2, 5).GetString());
            Assert.Equal("", zda.Cell(2, 6).GetString());
            Assert.Equal("正常", zda.Cell(2, 7).GetString());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
