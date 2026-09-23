using System;
using System.IO;
using System.Text.Json;

namespace OfficeCore.DesktopClient;

public enum DesktopMode
{
    None,
    Server,
    Client
}

public class DesktopConfig
{
    public DesktopMode SelectedMode { get; set; } = DesktopMode.None;
    public bool RememberChoice { get; set; } = false;
    public string ServerUrl { get; set; } = "http://localhost:5026";
    
    // Database Configuration for Server Station
    public string DbHost { get; set; } = "localhost";
    public int DbPort { get; set; } = 5432;
    public string DbName { get; set; } = "OfficeCoreDb";
    public string DbUser { get; set; } = "postgres";
    public string DbPassword { get; set; } = "password";

    public string BuildConnectionString()
    {
        return $"Host={DbHost};Port={DbPort};Database={DbName};Username={DbUser};Password={DbPassword};Include Error Detail=true";
    }
}

public static class DesktopConfigService
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktop_config.json");
    private static DesktopConfig? _current;

    public static DesktopConfig Current => _current ??= Load();

    public static DesktopConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<DesktopConfig>(json);
                if (config != null)
                {
                    _current = config;
                    return config;
                }
            }
        }
        catch (Exception)
        {
            // Fall back to default config if corrupted
        }

        _current = new DesktopConfig();
        return _current;
    }

    public static void Save(DesktopConfig config)
    {
        try
        {
            _current = config;
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception)
        {
            // Ignore failure writing config
        }
    }
}
