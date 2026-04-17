using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Application.Interfaces;

public enum BomExcelFileFormat
{
    Unknown = 0,
    Xls = 1,
    Xlsx = 2,
}

public interface IBomFileFormatDetector
{
    Result<BomExcelFileFormat> Detect(string filePath);
}

