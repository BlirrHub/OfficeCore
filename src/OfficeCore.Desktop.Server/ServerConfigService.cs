using System;
using System.IO;
using System.Text.Json;

namespace OfficeCore.Desktop.Server;

public class ServerConfig
{
    public string DbHost { get; set; } = "localhost";
    public int DbPort { get; set; } = 5432;
    public string DbName { get; set; } = GetDefaultDbName();
    public string DbUser { get; set; } = "postgres";
    public string DbPassword { get; set; } = "password";
    public bool AutoStartServer { get; set; } = false;
    public bool RunInBackground { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;

    // Optional Production SMTP Notification Settings
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "OfficeCore";

    public static string GetDefaultDbName()
    {
#if DEBUG
        try
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                var headFile = Path.Combine(dir, ".git", "HEAD");
                if (File.Exists(headFile))
                {
                    var head = File.ReadAllText(headFile);
                    if (head.Contains("refs/heads/development"))
                        return "officecoredevdb";
                    if (head.Contains("refs/heads/acba-dev"))
                        return "officecoreacbadevdb";
                    break;
                }
                dir = Directory.GetParent(dir)?.FullName;
            }
        }
        catch { }
#endif
        return "OfficeCoreDb";
    }

    public string BuildConnectionString()
    {
        return $"Host={DbHost};Port={DbPort};Database={DbName};Username={DbUser};Password={DbPassword};Include Error Detail=true";
    }
}

public static class ServerConfigService
{
    private static string GetConfigPath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "OfficeCore");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "server_config.json");
        }
        catch
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_config.json");
        }
    }

    private static ServerConfig? _current;

    public static ServerConfig Current => _current ??= Load();

    public static ServerConfig Load()
    {
        try
        {
            var path = GetConfigPath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<ServerConfig>(json);
                if (config != null)
                {
#if DEBUG
                    var defaultDb = ServerConfig.GetDefaultDbName();
                    if (config.DbName is "OfficeCoreDb" or "officecoredevdb" or "officecoreacbadevdb" || string.IsNullOrWhiteSpace(config.DbName))
                    {
                        config.DbName = defaultDb;
                    }
#endif
                    _current = config;
                    return config;
                }
            }
        }
        catch
        {
            // Fallback
        }

        _current = new ServerConfig();
        return _current;
    }

    public static void Save(ServerConfig config)
    {
        try
        {
            _current = config;
            var path = GetConfigPath();
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Ignore failure writing config
        }
    }

    public static void SetStartupWithWindows(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            const string appName = "OfficeCoreServerStation";
            if (enable)
            {
                var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(appName, $"\"{exePath}\" --background");
                }
            }
            else
            {
                key.DeleteValue(appName, false);
            }
        }
        catch { }
    }

    public static bool IsStartupWithWindowsEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", false);
            if (key == null) return false;
            return key.GetValue("OfficeCoreServerStation") != null;
        }
        catch { return false; }
    }
}
