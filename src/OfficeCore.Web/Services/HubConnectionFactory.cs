using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using OfficeCore.Web.Client.Auth;

namespace OfficeCore.Web.Services;

/// <summary>
/// Centralizes SignalR HubConnection creation with the cookie delegating handler and API base URI resolution.
/// </summary>
public class HubConnectionFactory
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;

    public HubConnectionFactory(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _configuration = configuration;
    }

    public HubConnection Create(string relativeHubPath, bool automaticReconnect = true)
    {
        var apiBase = _configuration["ApiBaseUrl"];
        var baseUri = _http.BaseAddress
            ?? (!string.IsNullOrWhiteSpace(apiBase)
                ? new Uri(apiBase.TrimEnd('/') + "/")
                : throw new InvalidOperationException("No API base address configured for SignalR hub connection."));

        var hubUrl = new Uri(baseUri, relativeHubPath.TrimStart('/'));

        var builder = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = handler => new CookieHandler { InnerHandler = handler };
            });

        if (automaticReconnect)
        {
            builder.WithAutomaticReconnect();
        }

        return builder.Build();
    }
}
