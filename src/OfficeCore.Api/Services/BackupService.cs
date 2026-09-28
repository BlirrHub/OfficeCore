using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace OfficeCore.Api.Services;

public class BackupFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsAutomatic { get; set; }
    public bool IsPreRestore { get; set; }
    public string? Label { get; set; }
}

public class BackupSettings
{
    public bool AutoBackupEnabled { get; set; } = true;
    public string ScheduledTime { get; set; } = "23:00"; // "HH:mm" in 24-hr format
    public int RetentionCount { get; set; } = 14;        // Keep last 14 backups
    public string? CustomPgBinPath { get; set; }
    public DateTime? LastBackupAt { get; set; }
    public string? LastBackupStatus { get; set; }
}

public class BackupResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public BackupFileInfo? BackupInfo { get; set; }
}

public class RestoreResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? PreRestoreSnapshotName { get; set; }
}

public class BackupService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupService> _logger;
    private readonly string _backupsDirectory;
    private readonly string _settingsFilePath;

    public BackupService(IConfiguration configuration, ILogger<BackupService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseFolder = Path.Combine(localAppData, "OfficeCore");
        _backupsDirectory = Path.Combine(baseFolder, "Backups");
        _settingsFilePath = Path.Combine(baseFolder, "backup_settings.json");

        if (!Directory.Exists(_backupsDirectory))
        {
            Directory.CreateDirectory(_backupsDirectory);
        }
    }

    public string GetBackupsDirectory() => _backupsDirectory;

    public string? DetectPgBinPath()
    {
        var settings = GetSettings();
        if (!string.IsNullOrWhiteSpace(settings.CustomPgBinPath) && Directory.Exists(settings.CustomPgBinPath))
        {
            var testDump = Path.Combine(settings.CustomPgBinPath, "pg_dump.exe");
            if (File.Exists(testDump)) return settings.CustomPgBinPath;
        }

        // Check standard PostgreSQL installation directories
        var standardRoots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PostgreSQL")
        };

        foreach (var root in standardRoots)
        {
            if (Directory.Exists(root))
            {
                var versionDirs = Directory.GetDirectories(root)
                    .OrderByDescending(d => Path.GetFileName(d));

                foreach (var vDir in versionDirs)
                {
                    var bin = Path.Combine(vDir, "bin");
                    if (File.Exists(Path.Combine(bin, "pg_dump.exe")) && File.Exists(Path.Combine(bin, "pg_restore.exe")))
                    {
                        return bin;
                    }
                }
            }
        }

        // Check relative to current process / portable bin
        var localBin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pgsql", "bin");
        if (File.Exists(Path.Combine(localBin, "pg_dump.exe")))
        {
            return localBin;
        }

        return null;
    }

    public BackupSettings GetSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<BackupSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read backup settings, falling back to defaults.");
        }

        return new BackupSettings();
    }

    public async Task SaveSettingsAsync(BackupSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save backup settings.");
            throw;
        }
    }

    public Task<List<BackupFileInfo>> GetBackupsAsync()
    {
        var list = new List<BackupFileInfo>();
        if (!Directory.Exists(_backupsDirectory))
            return Task.FromResult(list);

        var files = Directory.GetFiles(_backupsDirectory, "*.dump")
            .Concat(Directory.GetFiles(_backupsDirectory, "*.sql"))
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc);

        foreach (var f in files)
        {
            var name = f.Name;
            var isAuto = name.StartsWith("auto_", StringComparison.OrdinalIgnoreCase);
            var isPreRestore = name.StartsWith("prerestore_", StringComparison.OrdinalIgnoreCase);

            string? label = null;
            var metaPath = Path.Combine(_backupsDirectory, Path.GetFileNameWithoutExtension(name) + ".json");
            if (File.Exists(metaPath))
            {
                try
                {
                    var json = File.ReadAllText(metaPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Label", out var labelProp))
                    {
                        label = labelProp.GetString();
                    }
                }
                catch { }
            }

            list.Add(new BackupFileInfo
            {
                FileName = f.Name,
                FilePath = f.FullName,
                SizeBytes = f.Length,
                CreatedAt = f.CreationTimeUtc,
                IsAutomatic = isAuto,
                IsPreRestore = isPreRestore,
                Label = label
            });
        }

        return Task.FromResult(list);
    }

    public string? GetBackupFilePath(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(_backupsDirectory, safeFileName);
        return File.Exists(fullPath) ? fullPath : null;
    }

    public Task<bool> DeleteBackupAsync(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(_backupsDirectory, safeFileName);
        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                var metaPath = Path.Combine(_backupsDirectory, Path.GetFileNameWithoutExtension(safeFileName) + ".json");
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete backup file: {FileName}", fileName);
                return Task.FromResult(false);
            }
        }
        return Task.FromResult(false);
    }

    public async Task<BackupResult> CreateBackupAsync(string? label = null, bool isAutomatic = false, bool isPreRestore = false)
    {
        var pgBin = DetectPgBinPath();
        if (string.IsNullOrEmpty(pgBin))
        {
            return new BackupResult
            {
                Success = false,
                Message = "PostgreSQL tools (pg_dump.exe) could not be located. Please install PostgreSQL or configure the custom PostgreSQL bin path."
            };
        }

        var pgDumpExe = Path.Combine(pgBin, "pg_dump.exe");
        if (!File.Exists(pgDumpExe))
        {
            return new BackupResult
            {
                Success = false,
                Message = $"pg_dump.exe was not found in: {pgBin}"
            };
        }

        var connString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connString))
        {
            return new BackupResult { Success = false, Message = "DefaultConnection string is empty." };
        }

        var builder = new NpgsqlConnectionStringBuilder(connString);
        var host = string.IsNullOrWhiteSpace(builder.Host) ? "localhost" : builder.Host;
        var port = builder.Port > 0 ? builder.Port : 5432;
        var database = builder.Database ?? "OfficeCoreDb";
        var username = builder.Username ?? "postgres";
        var password = builder.Password ?? "";

        var prefix = isPreRestore ? "prerestore" : (isAutomatic ? "auto" : "manual");
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{prefix}_{timestamp}.dump";
        var fullPath = Path.Combine(_backupsDirectory, fileName);

        // Arguments for pg_dump custom archive format (-F c) with blobs (-b) and verbose (-v)
        var args = $"-h \"{host}\" -p {port} -U \"{username}\" -F c -b -v -f \"{fullPath}\" \"{database}\"";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pgDumpExe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.Environment["PGPASSWORD"] = password;

            _logger.LogInformation("Starting pg_dump to {Path}...", fullPath);

            using var process = new Process { StartInfo = psi };
            process.Start();

            var errorOutputTask = process.StandardError.ReadToEndAsync();
            var stdOutputTask = process.StandardOutput.ReadToEndAsync();

            var completed = await Task.Run(() => process.WaitForExit(300000)); // 5 minute timeout
            if (!completed)
            {
                try { process.Kill(); } catch { }
                return new BackupResult { Success = false, Message = "pg_dump timed out after 5 minutes." };
            }

            var stderr = await errorOutputTask;
            if (process.ExitCode != 0)
            {
                _logger.LogError("pg_dump failed with exit code {Code}: {Error}", process.ExitCode, stderr);
                if (File.Exists(fullPath)) { try { File.Delete(fullPath); } catch { } }
                return new BackupResult { Success = false, Message = $"pg_dump failed: {stderr}" };
            }

            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                return new BackupResult { Success = false, Message = "Backup file was created but is empty." };
            }

            // Save companion metadata json
            if (!string.IsNullOrWhiteSpace(label))
            {
                var metaPath = Path.Combine(_backupsDirectory, Path.GetFileNameWithoutExtension(fileName) + ".json");
                var meta = new { Label = label, CreatedAt = DateTime.UtcNow, IsAutomatic = isAutomatic, IsPreRestore = isPreRestore };
                await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(meta));
            }

            // Update settings last backup
            var settings = GetSettings();
            settings.LastBackupAt = DateTime.UtcNow;
            settings.LastBackupStatus = "Success";
            await SaveSettingsAsync(settings);

            // Execute retention pruning
            await PruneOldBackupsAsync(settings.RetentionCount);

            return new BackupResult
            {
                Success = true,
                Message = "Database backup created successfully.",
                BackupInfo = new BackupFileInfo
                {
                    FileName = fileName,
                    FilePath = fullPath,
                    SizeBytes = fileInfo.Length,
                    CreatedAt = fileInfo.CreationTimeUtc,
                    IsAutomatic = isAutomatic,
                    IsPreRestore = isPreRestore,
                    Label = label
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while executing pg_dump.");
            return new BackupResult { Success = false, Message = $"Exception: {ex.Message}" };
        }
    }

    public async Task<RestoreResult> RestoreBackupAsync(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(_backupsDirectory, safeFileName);
        if (!File.Exists(fullPath))
        {
            return new RestoreResult { Success = false, Message = $"Backup file not found: {fileName}" };
        }

        return await RestoreFromFilePathInternalAsync(fullPath);
    }

    public async Task<RestoreResult> RestoreFromFileAsync(string tempFilePath)
    {
        if (!File.Exists(tempFilePath))
        {
            return new RestoreResult { Success = false, Message = "Uploaded file does not exist on server." };
        }

        // Move/copy into backups directory with clear name
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var permanentFileName = $"uploaded_{timestamp}.dump";
        var permanentPath = Path.Combine(_backupsDirectory, permanentFileName);
        File.Copy(tempFilePath, permanentPath, overwrite: true);

        return await RestoreFromFilePathInternalAsync(permanentPath);
    }

    private async Task<RestoreResult> RestoreFromFilePathInternalAsync(string dumpFilePath)
    {
        var pgBin = DetectPgBinPath();
        if (string.IsNullOrEmpty(pgBin))
        {
            return new RestoreResult
            {
                Success = false,
                Message = "PostgreSQL tools (pg_restore.exe) could not be located. Please install PostgreSQL or configure bin path."
            };
        }

        var pgRestoreExe = Path.Combine(pgBin, "pg_restore.exe");
        if (!File.Exists(pgRestoreExe))
        {
            return new RestoreResult { Success = false, Message = $"pg_restore.exe was not found in: {pgBin}" };
        }

        // STEP 1: Pre-Restore Safety Snapshot!
        _logger.LogInformation("Taking safety pre-restore snapshot before applying {Dump}...", dumpFilePath);
        var preSnapshot = await CreateBackupAsync("Automatic safety snapshot prior to restore", isAutomatic: false, isPreRestore: true);
        string? preSnapshotName = preSnapshot.BackupInfo?.FileName;

        // STEP 2: Clear EF Core Connection Pools
        NpgsqlConnection.ClearAllPools();

        var connString = _configuration.GetConnectionString("DefaultConnection");
        var builder = new NpgsqlConnectionStringBuilder(connString);
        var host = string.IsNullOrWhiteSpace(builder.Host) ? "localhost" : builder.Host;
        var port = builder.Port > 0 ? builder.Port : 5432;
        var database = builder.Database ?? "OfficeCoreDb";
        var username = builder.Username ?? "postgres";
        var password = builder.Password ?? "";

        // Arguments for pg_restore:
        // -d {database} : Target db
        // -c : Clean (drop database objects before recreating)
        // --if-exists : Don't fail if object doesn't exist when dropping
        // -v : Verbose
        var args = $"-h \"{host}\" -p {port} -U \"{username}\" -d \"{database}\" -c --if-exists -v \"{dumpFilePath}\"";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pgRestoreExe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.Environment["PGPASSWORD"] = password;

            _logger.LogInformation("Starting pg_restore from {Path}...", dumpFilePath);

            using var process = new Process { StartInfo = psi };
            process.Start();

            var errorOutputTask = process.StandardError.ReadToEndAsync();
            var stdOutputTask = process.StandardOutput.ReadToEndAsync();

            var completed = await Task.Run(() => process.WaitForExit(300000)); // 5 minute timeout
            if (!completed)
            {
                try { process.Kill(); } catch { }
                return new RestoreResult
                {
                    Success = false,
                    Message = "pg_restore timed out after 5 minutes.",
                    PreRestoreSnapshotName = preSnapshotName
                };
            }

            var stderr = await errorOutputTask;

            // In pg_restore, exit code 1 is frequently issued for non-fatal drop warnings
            // (e.g., relation did not exist to drop). Exit code 0 or 1 with non-critical warnings is acceptable.
            if (process.ExitCode > 1)
            {
                _logger.LogError("pg_restore failed with exit code {Code}: {Error}", process.ExitCode, stderr);
                return new RestoreResult
                {
                    Success = false,
                    Message = $"Restore failed with code {process.ExitCode}: {stderr}",
                    PreRestoreSnapshotName = preSnapshotName
                };
            }

            // Clear pools again after restore
            NpgsqlConnection.ClearAllPools();

            _logger.LogInformation("pg_restore completed successfully.");
            return new RestoreResult
            {
                Success = true,
                Message = "Database successfully restored from backup.",
                PreRestoreSnapshotName = preSnapshotName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while executing pg_restore.");
            return new RestoreResult
            {
                Success = false,
                Message = $"Restore exception: {ex.Message}",
                PreRestoreSnapshotName = preSnapshotName
            };
        }
    }

    private async Task PruneOldBackupsAsync(int retentionCount)
    {
        if (retentionCount <= 0) return;

        try
        {
            var backups = await GetBackupsAsync();
            // Prune automatic backups that exceed retention count
            var autoBackups = backups
                .Where(b => b.IsAutomatic)
                .OrderByDescending(b => b.CreatedAt)
                .ToList();

            if (autoBackups.Count > retentionCount)
            {
                var toDelete = autoBackups.Skip(retentionCount);
                foreach (var b in toDelete)
                {
                    await DeleteBackupAsync(b.FileName);
                    _logger.LogInformation("Pruned old automatic backup: {FileName}", b.FileName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while pruning old backups.");
        }
    }
}
