using System.IO;

namespace MaterialCodingSystem.Application.Logging;

/// <summary>日志中的路径脱敏：仅文件名。</summary>
public static class McsLog
{
    public static string? FileNameForLog(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            return Path.GetFileName(path.Trim());
        }
        catch
        {
            return "(invalid_path)";
        }
    }
}
