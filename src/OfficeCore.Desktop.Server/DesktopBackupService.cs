using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OfficeCore.Desktop.Server;

public class BackupItemViewModel
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FormattedSize => FormatBytes(SizeBytes);
    public DateTime CreatedAt { get; set; }
    public string FormattedDate => CreatedAt.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt");
    public bool IsAutomatic { get; set; }
    public bool IsPreRestore { get; set; }
    public string TypeBadge => IsPreRestore ? "SAFETY SNAPSHOT" : (IsAutomatic ? "AUTO SCHEDULE" : "MANUAL SNAPSHOT");
    public string TypeColor => IsPreRestore ? "#818CF8" : (IsAutomatic ? "#34D399" : "#38BDF8");
    public string TypeBg => IsPreRestore ? "#1E1B4B" : (IsAutomatic ? "#064E3B" : "#082F49");
    public string? Label { get; set; }
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? "Standard system database snapshot" : Label;

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

public class BackupSettingsData
{
    public bool AutoBackupEnabled { get; set; } = true;
    public string ScheduledTime { get; set; } = "23:00";
    public int RetentionCount { get; set; } = 14;
    public string? CustomPgBinPath { get; set; }
    public DateTime? LastBackupAt { get; set; }
    public string? LastBackupStatus { get; set; }
}

public class BackupDashboardData
{
    public List<BackupItemViewModel> Backups { get; set; } = new();
    public string? PgBinPath { get; set; }
    public bool IsPgToolsAvailable { get; set; }
    public long TotalSizeBytes { get; set; }
    public BackupSettingsData Settings { get; set; } = new();
}

public class DesktopBackupService
{
    private static readonly string BackupsFolder = Path.Combine(
        // Profile-scoped path resolved via ServerConfigService.GetProfileFolder()
        ServerConfigService.GetProfileFolder(), "Backups");

    private static readonly string SettingsFile = Path.Combine(
        // Profile-scoped path resolved via ServerConfigService.GetProfileFolder()
        ServerConfigService.GetProfileFolder(), "backup_settings.json");

    private HttpClient CreateClient(TimeSpan? timeout = null)
    {
        var client = new HttpClient { Timeout = timeout ?? TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.Add("X-OfficeCore-Client", "OfficeCore-Desktop-Workstation");
        client.DefaultRequestHeaders.Add("X-OfficeCore-Station", "ServerStationOrchestrator");
        client.DefaultRequestHeaders.Add("X-OfficeCore-Station-Token", ServerConfigService.GetOrCreateStationToken());
        return client;
    }

    public string GetBackupsDirectory()
    {
        if (!Directory.Exists(BackupsFolder))
        {
            Directory.CreateDirectory(BackupsFolder);
        }
        return BackupsFolder;
    }

    public void OpenBackupsFolderInExplorer()
    {
        var folder = GetBackupsDirectory();
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    public async Task<BackupDashboardData> LoadDashboardDataAsync(bool isServerRunning)
    {
        if (isServerRunning)
        {
            try
            {
                using var client = CreateClient(TimeSpan.FromSeconds(5));
                var response = await client.GetAsync("http://localhost:5026/api/backup");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var data = new BackupDashboardData();
                    if (root.TryGetProperty("pgBinPath", out var binProp))
                        data.PgBinPath = binProp.GetString();
                    if (root.TryGetProperty("isPgToolsAvailable", out var availProp))
                        data.IsPgToolsAvailable = availProp.GetBoolean();
                    if (root.TryGetProperty("totalSizeBytes", out var sizeProp))
                        data.TotalSizeBytes = sizeProp.GetInt64();

                    if (root.TryGetProperty("settings", out var setProp))
                    {
                        data.Settings = JsonSerializer.Deserialize<BackupSettingsData>(setProp.GetRawText()) ?? new();
                    }

                    if (root.TryGetProperty("backups", out var bListProp) && bListProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in bListProp.EnumerateArray())
                        {
                            data.Backups.Add(new BackupItemViewModel
                            {
                                FileName = el.GetProperty("fileName").GetString() ?? "",
                                FilePath = el.GetProperty("filePath").GetString() ?? "",
                                SizeBytes = el.GetProperty("sizeBytes").GetInt64(),
                                CreatedAt = el.GetProperty("createdAt").GetDateTime(),
                                IsAutomatic = el.GetProperty("isAutomatic").GetBoolean(),
                                IsPreRestore = el.GetProperty("isPreRestore").GetBoolean(),
                                Label = el.TryGetProperty("label", out var l) ? l.GetString() : null
                            });
                        }
                    }

                    return data;
                }
            }
            catch
            {
                // Fall back to direct local disk inspection
            }
        }

        return LoadDataFromDisk();
    }

    public BackupDashboardData LoadDataFromDisk()
    {
        var data = new BackupDashboardData();
        var folder = GetBackupsDirectory();

        if (Directory.Exists(folder))
        {
            var files = Directory.GetFiles(folder, "*.dump")
                .Concat(Directory.GetFiles(folder, "*.sql"))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTimeUtc);

            foreach (var f in files)
            {
                var isAuto = f.Name.StartsWith("auto_", StringComparison.OrdinalIgnoreCase);
                var isPre = f.Name.StartsWith("prerestore_", StringComparison.OrdinalIgnoreCase);
                string? label = null;

                var metaPath = Path.Combine(folder, Path.GetFileNameWithoutExtension(f.Name) + ".json");
                if (File.Exists(metaPath))
                {
                    try
                    {
                        var metaJson = File.ReadAllText(metaPath);
                        using var doc = JsonDocument.Parse(metaJson);
                        if (doc.RootElement.TryGetProperty("Label", out var lp))
                            label = lp.GetString();
                    }
                    catch { }
                }

                data.Backups.Add(new BackupItemViewModel
                {
                    FileName = f.Name,
                    FilePath = f.FullName,
                    SizeBytes = f.Length,
                    CreatedAt = f.CreationTimeUtc,
                    IsAutomatic = isAuto,
                    IsPreRestore = isPre,
                    Label = label
                });
            }

            data.TotalSizeBytes = data.Backups.Sum(b => b.SizeBytes);
        }

        if (File.Exists(SettingsFile))
        {
            try
            {
                var sJson = File.ReadAllText(SettingsFile);
                data.Settings = JsonSerializer.Deserialize<BackupSettingsData>(sJson) ?? new();
            }
            catch { }
        }

        data.PgBinPath = DetectLocalPgBin();
        data.IsPgToolsAvailable = !string.IsNullOrEmpty(data.PgBinPath);

        return data;
    }

    public string? DetectLocalPgBin()
    {
        var standardRoots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PostgreSQL")
        };

        foreach (var root in standardRoots)
        {
            if (Directory.Exists(root))
            {
                var versionDirs = Directory.GetDirectories(root).OrderByDescending(d => Path.GetFileName(d));
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

        var localBin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pgsql", "bin");
        if (File.Exists(Path.Combine(localBin, "pg_dump.exe")))
        {
            return localBin;
        }

        return null;
    }

    public async Task<(bool success, string message)> CreateSnapshotAsync(string? label)
    {
        try
        {
            using var client = CreateClient(TimeSpan.FromMinutes(5));
            var payload = JsonSerializer.Serialize(new { Label = label });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("http://localhost:5026/api/backup/create", content);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Database snapshot created successfully!");
            }

            var err = await response.Content.ReadAsStringAsync();
            return (false, $"Failed to create snapshot: {err}");
        }
        catch (Exception ex)
        {
            return (false, $"Error creating snapshot: {ex.Message}");
        }
    }

    public async Task<(bool success, string message)> RestoreSnapshotAsync(string fileName)
    {
        try
        {
            using var client = CreateClient(TimeSpan.FromMinutes(6));
            var response = await client.PostAsync($"http://localhost:5026/api/backup/{Uri.EscapeDataString(fileName)}/restore", null);

            if (response.IsSuccessStatusCode)
            {
                return (true, "Database restored successfully!");
            }

            var err = await response.Content.ReadAsStringAsync();
            return (false, $"Restore failed: {err}");
        }
        catch (Exception ex)
        {
            return (false, $"Error executing restore: {ex.Message}");
        }
    }

    public async Task<(bool success, string message)> UploadAndRestoreAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return (false, "Specified file does not exist.");

        try
        {
            using var client = CreateClient(TimeSpan.FromMinutes(10));
            using var form = new MultipartFormDataContent();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var streamContent = new StreamContent(fileStream);

            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(streamContent, "file", Path.GetFileName(filePath));

            var response = await client.PostAsync("http://localhost:5026/api/backup/upload", form);
            if (response.IsSuccessStatusCode)
            {
                return (true, "External backup uploaded and restored successfully!");
            }

            var err = await response.Content.ReadAsStringAsync();
            return (false, $"Import and restore failed: {err}");
        }
        catch (Exception ex)
        {
            return (false, $"Error uploading backup: {ex.Message}");
        }
    }

    public async Task<bool> DeleteSnapshotAsync(string fileName)
    {
        try
        {
            using var client = CreateClient(TimeSpan.FromSeconds(10));
            var response = await client.DeleteAsync($"http://localhost:5026/api/backup/{Uri.EscapeDataString(fileName)}");
            if (response.IsSuccessStatusCode) return true;
        }
        catch { }

        // Fallback to disk delete
        try
        {
            var folder = GetBackupsDirectory();
            var fullPath = Path.Combine(folder, Path.GetFileName(fileName));
            if (File.Exists(fullPath)) File.Delete(fullPath);

            var meta = Path.Combine(folder, Path.GetFileNameWithoutExtension(fileName) + ".json");
            if (File.Exists(meta)) File.Delete(meta);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SaveSettingsAsync(BackupSettingsData settings)
    {
        try
        {
            using var client = CreateClient(TimeSpan.FromSeconds(5));
            var payload = JsonSerializer.Serialize(settings);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PutAsync("http://localhost:5026/api/backup/settings", content);
            if (response.IsSuccessStatusCode) return true;
        }
        catch { }

        // Fallback to saving directly to settings file
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(SettingsFile, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
