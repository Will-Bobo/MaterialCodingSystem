using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Infrastructure.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

public class SqliteUniqueConstraintMappingTests
{
    [Theory]
    [InlineData(
        "SQLite Error 19: 'UNIQUE constraint failed: material_item.category_code, material_item.spec'",
        IMaterialRepository.CONSTRAINT_ITEM_CATEGORY_SPEC)]
    [InlineData(
        "UNIQUE constraint failed: material_item.spec, material_item.category_code",
        IMaterialRepository.CONSTRAINT_ITEM_CATEGORY_SPEC)]
    [InlineData(
        "UNIQUE constraint failed: material_item.group_id, material_item.suffix",
        IMaterialRepository.CONSTRAINT_ITEM_GROUP_SUFFIX)]
    [InlineData(
        "unique constraint failed: MATERIAL_ITEM.GROUP_ID, MATERIAL_ITEM.SUFFIX",
        IMaterialRepository.CONSTRAINT_ITEM_GROUP_SUFFIX)]
    [InlineData(
        "UNIQUE constraint failed: material_item.code",
        IMaterialRepository.CONSTRAINT_ITEM_CODE)]
    [InlineData(
        "UNIQUE constraint failed: material_group.category_id, material_group.serial_no",
        IMaterialRepository.CONSTRAINT_GROUP_CATEGORY_SERIAL)]
    [InlineData("", IMaterialRepository.CONSTRAINT_UNKNOWN)]
    [InlineData("UNIQUE constraint failed: other.table", IMaterialRepository.CONSTRAINT_UNKNOWN)]
    public void MapSqliteUniqueConstraintViolation_Classifies_By_Table_And_Columns(string message, string expected)
    {
        Assert.Equal(expected, SqliteMaterialRepository.MapSqliteUniqueConstraintViolation(message));
    }
}
