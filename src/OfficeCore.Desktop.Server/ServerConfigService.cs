using System;
using System.IO;
using System.Text.Json;

namespace OfficeCore.Desktop.Server;

public class ServerConfig
{
    public string DbHost { get; set; } = "localhost";
    public int DbPort { get; set; } = 5432;
    public string DbName { get; set; } = "OfficeCoreDb";
    public string DbUser { get; set; } = "postgres";
    public string DbPassword { get; set; } = "password";
    public bool AutoStartServer { get; set; } = false;

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
}
