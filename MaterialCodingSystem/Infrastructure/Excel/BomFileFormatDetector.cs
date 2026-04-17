using System.IO;
using System.IO.Compression;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Infrastructure.Excel;

/// <summary>
/// BOM 文件真实性检测：不依赖扩展名，通过文件头结构判断 xls/xlsx。
/// </summary>
public sealed class BomFileFormatDetector : IBomFileFormatDetector
{
    private static readonly byte[] OLE_XLS_SIG = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

    public Result<BomExcelFileFormat> Detect(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Result<BomExcelFileFormat>.Fail(ErrorCodes.VALIDATION_ERROR, "file_path is required.");

        if (!File.Exists(filePath))
            return Result<BomExcelFileFormat>.Fail(ErrorCodes.NOT_FOUND, "file not found.");

        try
        {
            using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> head = stackalloc byte[8];
            var read = fs.Read(head);
            if (read < 4)
                return Result<BomExcelFileFormat>.Ok(BomExcelFileFormat.Unknown);

            if (read >= 8 && head.SequenceEqual(OLE_XLS_SIG))
                return Result<BomExcelFileFormat>.Ok(BomExcelFileFormat.Xls);

            // ZIP signature: PK..
            if (head[0] == (byte)'P' && head[1] == (byte)'K')
            {
                fs.Position = 0;
                using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
                // xlsx 必须包含该 entry
                var entry = zip.GetEntry("xl/workbook.xml");
                if (entry is not null)
                    return Result<BomExcelFileFormat>.Ok(BomExcelFileFormat.Xlsx);
            }

            return Result<BomExcelFileFormat>.Ok(BomExcelFileFormat.Unknown);
        }
        catch (InvalidDataException)
        {
            return Result<BomExcelFileFormat>.Ok(BomExcelFileFormat.Unknown);
        }
        catch (Exception)
        {
            return Result<BomExcelFileFormat>.Ok(BomExcelFileFormat.Unknown);
        }
    }
}

