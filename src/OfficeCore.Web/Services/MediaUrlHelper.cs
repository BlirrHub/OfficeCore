using System;
using Microsoft.Extensions.Configuration;

namespace OfficeCore.Web.Services;

public static class MediaUrlHelper
{
    public static string Resolve(string? path, string? apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var baseAddress = (apiBaseUrl ?? string.Empty).TrimEnd('/');
        var relative = path.StartsWith('/') ? path : "/" + path;
        return $"{baseAddress}{relative}";
    }

    public static string Resolve(string? path, IConfiguration config)
    {
        return Resolve(path, config["ApiBaseUrl"]);
    }
}
