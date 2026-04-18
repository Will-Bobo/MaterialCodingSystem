using System.IO;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Application.Logging;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Infrastructure.Storage;

public sealed class FileSystemBomArchiveStorage : IFileSystemBomArchiveStorage
{
    private readonly ILogger<FileSystemBomArchiveStorage> _logger;

    public FileSystemBomArchiveStorage(ILogger<FileSystemBomArchiveStorage>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemBomArchiveStorage>.Instance;
    }

    public async Task CopyToArchiveAsync(string sourceFilePath, string finalPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("sourceFilePath is required.", nameof(sourceFilePath));
        if (string.IsNullOrWhiteSpace(finalPath))
            throw new ArgumentException("finalPath is required.", nameof(finalPath));

        ct.ThrowIfCancellationRequested();

        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("source file not found.", sourceFilePath);

        var dir = Path.GetDirectoryName(finalPath);
        if (string.IsNullOrWhiteSpace(dir))
            throw new IOException("finalPath has no directory.");

        var tmpPath = finalPath + ".tmp";
        try
        {
            // ensure no leftovers
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);

            Directory.CreateDirectory(dir);

            await using (var src = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var dst = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await src.CopyToAsync(dst, 1024 * 128, ct);
                await dst.FlushAsync(ct);
            }

            // do NOT overwrite existing
            File.Move(tmpPath, finalPath, overwrite: false);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
            }
            catch
            {
                // swallow cleanup failure
            }

            McsLoggingExtensions.LogException(_logger, ex, McsActions.BomArchiveService, ErrorCodes.INTERNAL_ERROR);
            throw;
        }
    }

    public async Task CopyToArchiveOverwriteAsync(string sourceFilePath, string finalPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("sourceFilePath is required.", nameof(sourceFilePath));
        if (string.IsNullOrWhiteSpace(finalPath))
            throw new ArgumentException("finalPath is required.", nameof(finalPath));

        ct.ThrowIfCancellationRequested();

        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("source file not found.", sourceFilePath);

        var dir = Path.GetDirectoryName(finalPath);
        if (string.IsNullOrWhiteSpace(dir))
            throw new IOException("finalPath has no directory.");

        var tmpPath = finalPath + ".tmp";
        try
        {
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);

            Directory.CreateDirectory(dir);

            await using (var src = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var dst = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await src.CopyToAsync(dst, 1024 * 128, ct);
                await dst.FlushAsync(ct);
            }

            if (File.Exists(finalPath))
            {
                // atomic replace on same volume
                File.Replace(tmpPath, finalPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tmpPath, finalPath, overwrite: false);
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
            }
            catch
            {
                // swallow cleanup failure
            }

            McsLoggingExtensions.LogException(_logger, ex, McsActions.BomArchiveService, ErrorCodes.INTERNAL_ERROR);
            throw;
        }
    }

    public Task DeleteIfExistsAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }
}

