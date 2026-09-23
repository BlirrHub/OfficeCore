using System;
using System.IO;
using System.Text.Json;

namespace OfficeCore.Desktop.Client;

public class ClientConfig
{
    public string ServerUrl { get; set; } = "http://127.0.0.1:5026";
    public bool AutoConnect { get; set; } = false;
}

public static class ClientConfigService
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
            return Path.Combine(folder, "client_config.json");
        }
        catch
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client_config.json");
        }
    }

    private static ClientConfig? _current;

    public static ClientConfig Current => _current ??= Load();

    public static ClientConfig Load()
    {
        try
        {
            var path = GetConfigPath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<ClientConfig>(json);
                if (config != null)
                {
                    _current = config;
                    return config;
                }
            }
        }
        catch
        {
            // Fallback to default
        }

        _current = new ClientConfig();
        return _current;
    }

    public static void Save(ClientConfig config)
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
