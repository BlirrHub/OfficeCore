using System;
using System.IO;
using System.Data.Common;
using System.Linq;
using System.Reflection;
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

    // Embedded in both Debug and Release builds; never infer deployment from .git.
    public static string GetDefaultDbName()
    {
        return typeof(ServerConfig).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "OfficeCoreDefaultDatabase").Value
            ?? throw new InvalidOperationException("The deployment database profile is missing.");
    }

    public string BuildConnectionString()
    {
        var defaultDatabase = GetDefaultDbName();
        var reservedDatabases = new[] { "OfficeCoreDb", "officecoredevdb", "officecoreacbadevdb" };
        if (reservedDatabases.Contains(DbName, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(DbName, defaultDatabase, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This build cannot use another deployment's reserved database.");
        }

        var connection = new DbConnectionStringBuilder
        {
            ["Host"] = DbHost,
            ["Port"] = DbPort,
            ["Database"] = DbName,
            ["Username"] = DbUser,
            ["Password"] = DbPassword,
            ["Include Error Detail"] = true
        };
        return connection.ConnectionString;
    }
}

public static class ServerConfigService
{
    private static string GetConfigPath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var defaultDatabase = ServerConfig.GetDefaultDbName();
            // Keep the production path compatible; isolate development settings.
            var folder = defaultDatabase == "OfficeCoreDb"
                ? Path.Combine(appData, "OfficeCore")
                : Path.Combine(appData, "OfficeCore", defaultDatabase);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "server_config.json");
        }
        catch
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"server_config.{ServerConfig.GetDefaultDbName()}.json");
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
