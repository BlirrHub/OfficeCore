using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OfficeCore.Api.Services;

public record StoredFileResult(bool Success, string? ErrorMessage, string? RelativeUrl);

public record ResolvedStoredFile(string FullPath, string ContentType, bool AllowInline);

public interface IFileStorageService
{
    Task<StoredFileResult> ValidateAndSaveAsync(
        IFormFile? file,
        string category,
        string targetFileName,
        IReadOnlyCollection<string> allowedExtensions,
        long maxBytes);

    ResolvedStoredFile? ResolveFile(string category, string fileName);

    void MigrateLegacyWebRootUploads(string? webRootPath);
}

public sealed class LocalFileStorageService : IFileStorageService
{
    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "profiles", "organizations", "leaves", "chat", "boards"
    };

    private static readonly Dictionary<string, (string Mime, bool Inline)> SafeMimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ("image/jpeg", true),
        [".jpeg"] = ("image/jpeg", true),
        [".png"] = ("image/png", true),
        [".gif"] = ("image/gif", true),
        [".webp"] = ("image/webp", true),
        [".pdf"] = ("application/pdf", true),
        [".txt"] = ("text/plain; charset=utf-8", false),
        [".csv"] = ("text/csv; charset=utf-8", false),
        [".doc"] = ("application/msword", false),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", false),
        [".xls"] = ("application/vnd.ms-excel", false),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", false),
        [".ppt"] = ("application/vnd.ms-powerpoint", false),
        [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", false),
        [".zip"] = ("application/zip", false),
        [".rar"] = ("application/vnd.rar", false),
        [".7z"] = ("application/x-7z-compressed", false),
        [".mp4"] = ("video/mp4", true),
        [".mp3"] = ("audio/mpeg", true),
        [".wav"] = ("audio/wav", true)
    };

    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorageService>? _logger;

    public LocalFileStorageService(IConfiguration? configuration = null, ILogger<LocalFileStorageService>? logger = null)
    {
        _logger = logger;
        var configured = configuration?["Uploads:RootPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _rootPath = Path.GetFullPath(configured);
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var baseDir = !string.IsNullOrWhiteSpace(localAppData)
                ? Path.Combine(localAppData, "OfficeCore")
                : Directory.GetCurrentDirectory();
            _rootPath = Path.GetFullPath(Path.Combine(baseDir, "Uploads"));
        }

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFileResult> ValidateAndSaveAsync(
        IFormFile? file,
        string category,
        string targetFileName,
        IReadOnlyCollection<string> allowedExtensions,
        long maxBytes)
    {
        if (!ValidCategories.Contains(category))
            return new StoredFileResult(false, "Invalid upload category.", null);

        if (file == null || file.Length == 0)
            return new StoredFileResult(false, "File is empty.", null);

        if (file.Length > maxBytes)
            return new StoredFileResult(false, $"File exceeds the {maxBytes / (1024 * 1024)}MB limit.", null);

        var safeName = Path.GetFileName(targetFileName);
        if (string.IsNullOrWhiteSpace(safeName) || !string.Equals(safeName, targetFileName, StringComparison.Ordinal))
            return new StoredFileResult(false, "Invalid target file name.", null);

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (!allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return new StoredFileResult(false, $"Unsupported file type '{ext}'.", null);

        byte[] header = new byte[64];
        int headerRead;
        await using (var peekStream = file.OpenReadStream())
        {
            headerRead = await peekStream.ReadAsync(header.AsMemory(0, header.Length));
        }

        if (!MatchesExpectedMagicBytes(ext, header.AsSpan(0, headerRead)))
            return new StoredFileResult(false, $"File contents do not match the '{ext}' format.", null);

        var categoryDir = Path.Combine(_rootPath, category.ToLowerInvariant());
        Directory.CreateDirectory(categoryDir);

        var destPath = Path.Combine(categoryDir, safeName);
        await using (var outStream = new FileStream(destPath, FileMode.Create))
        {
            await file.CopyToAsync(outStream);
        }

        return new StoredFileResult(true, null, $"/uploads/{category.ToLowerInvariant()}/{safeName}");
    }

    public ResolvedStoredFile? ResolveFile(string category, string fileName)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(fileName))
            return null;

        if (!ValidCategories.Contains(category))
            return null;

        var safeName = Path.GetFileName(fileName);
        if (!string.Equals(safeName, fileName, StringComparison.Ordinal) || safeName.Contains("..", StringComparison.Ordinal))
            return null;

        var ext = Path.GetExtension(safeName);
        if (!SafeMimeMap.TryGetValue(ext, out var mimeInfo))
            return null;

        var categoryRoot = Path.GetFullPath(Path.Combine(_rootPath, category.ToLowerInvariant()));
        var fullPath = Path.GetFullPath(Path.Combine(categoryRoot, safeName));
        if (!fullPath.StartsWith(categoryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!File.Exists(fullPath))
            return null;

        return new ResolvedStoredFile(fullPath, mimeInfo.Mime, mimeInfo.Inline);
    }

    public void MigrateLegacyWebRootUploads(string? webRootPath)
    {
        if (string.IsNullOrWhiteSpace(webRootPath))
            return;

        var legacyRoot = Path.Combine(webRootPath, "uploads");
        if (!Directory.Exists(legacyRoot))
            return;

        foreach (var category in ValidCategories)
        {
            var legacyCat = Path.Combine(legacyRoot, category);
            if (!Directory.Exists(legacyCat)) continue;

            var destCat = Path.Combine(_rootPath, category);
            Directory.CreateDirectory(destCat);

            foreach (var srcFile in Directory.EnumerateFiles(legacyCat))
            {
                var name = Path.GetFileName(srcFile);
                var destFile = Path.Combine(destCat, name);
                try
                {
                    if (!File.Exists(destFile))
                        File.Move(srcFile, destFile);
                    else
                        File.Delete(srcFile);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not migrate legacy upload {FilePath}", srcFile);
                }
            }

            try
            {
                if (!Directory.EnumerateFileSystemEntries(legacyCat).Any())
                    Directory.Delete(legacyCat);
            }
            catch { }
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(legacyRoot).Any())
                Directory.Delete(legacyRoot);
        }
        catch { }
    }

    private static bool MatchesExpectedMagicBytes(string ext, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return false;

        return ext switch
        {
            ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ".png" => bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                      && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            ".gif" => bytes.Length >= 6 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == '8'
                      && (bytes[4] == '7' || bytes[4] == '9') && bytes[5] == 'a',
            ".webp" => bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
                       && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P',
            ".pdf" => TrimBomAndLeadingWhitespace(bytes) is var pdf && pdf.Length >= 5
                      && pdf[0] == '%' && pdf[1] == 'P' && pdf[2] == 'D' && pdf[3] == 'F' && pdf[4] == '-',
            ".zip" or ".docx" or ".xlsx" or ".pptx" => bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B
                      && ((bytes[2] == 0x03 && bytes[3] == 0x04) || (bytes[2] == 0x05 && bytes[3] == 0x06) || (bytes[2] == 0x07 && bytes[3] == 0x08)),
            ".doc" or ".xls" or ".ppt" => bytes.Length >= 8 && bytes[0] == 0xD0 && bytes[1] == 0xCF && bytes[2] == 0x11 && bytes[3] == 0xE0
                      && bytes[4] == 0xA1 && bytes[5] == 0xB1 && bytes[6] == 0x1A && bytes[7] == 0xE1,
            ".rar" => bytes.Length >= 4 && bytes[0] == 'R' && bytes[1] == 'a' && bytes[2] == 'r' && bytes[3] == '!',
            ".7z" => bytes.Length >= 6 && bytes[0] == 0x37 && bytes[1] == 0x7A && bytes[2] == 0xBC && bytes[3] == 0xAF && bytes[4] == 0x27 && bytes[5] == 0x1C,
            ".wav" => bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
                      && bytes[8] == 'W' && bytes[9] == 'A' && bytes[10] == 'V' && bytes[11] == 'E',
            ".mp4" => bytes.Length >= 8 && bytes[4] == 'f' && bytes[5] == 't' && bytes[6] == 'y' && bytes[7] == 'p',
            ".mp3" => (bytes.Length >= 3 && bytes[0] == 'I' && bytes[1] == 'D' && bytes[2] == '3')
                      || (bytes.Length >= 2 && bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0),
            ".txt" or ".csv" => !bytes.Contains((byte)0) && !(bytes.Length >= 2 && bytes[0] == 'M' && bytes[1] == 'Z'),
            _ => false
        };
    }

    private static ReadOnlySpan<byte> TrimBomAndLeadingWhitespace(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            bytes = bytes[3..];
        int idx = 0;
        while (idx < bytes.Length && (bytes[idx] == ' ' || bytes[idx] == '\t' || bytes[idx] == '\r' || bytes[idx] == '\n'))
            idx++;
        return bytes[idx..];
    }
}
