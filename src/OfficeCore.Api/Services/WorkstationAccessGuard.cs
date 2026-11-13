namespace OfficeCore.Api.Services;

/// <summary>
/// Enforces desktop-workstation-only access to the hosted web UI on production builds,
/// returning 403 Forbidden for direct generic web browser requests while preserving
/// API, SignalR, and official desktop WebView2 client traffic.
/// </summary>
public static class WorkstationAccessGuard
{
    public const string ClientHeaderName = "X-OfficeCore-Client";
    public const string ExpectedClientToken = "OfficeCore-Desktop-Workstation";

    private static readonly string[] AllowedAssetExtensions =
    [
        ".js", ".css", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".ico", ".woff", ".woff2", ".wasm", ".dat", ".json"
    ];

    private const string RestrictedHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8"/>
            <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
            <title>Access Restricted — OfficeCore</title>
            <style>
                body { margin: 0; background: #09090b; color: #f4f4f5; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; display: flex; align-items: center; justify-content: center; height: 100vh; }
                .card { background: #18181b; border: 1px solid #27272a; border-radius: 16px; padding: 40px; max-width: 480px; text-align: center; box-shadow: 0 25px 50px -12px rgba(0,0,0,0.5); }
                .badge { display: inline-block; background: #7f1d1d; color: #fca5a5; font-size: 11px; font-weight: 700; text-transform: uppercase; padding: 4px 10px; border-radius: 9999px; margin-bottom: 16px; }
                h1 { font-size: 22px; font-weight: 700; margin: 0 0 10px 0; color: #ffffff; }
                p { font-size: 14px; line-height: 1.6; color: #a1a1aa; margin: 0 0 24px 0; }
                .instruction { background: #09090b; border: 1px solid #27272a; border-radius: 8px; padding: 14px; font-size: 13px; color: #38bdf8; text-align: left; }
            </style>
        </head>
        <body>
            <div class="card">
                <div class="badge">Browser Access Restricted</div>
                <h1>Official Client Required</h1>
                <p>Direct web browser access to OfficeCore is disabled on production servers.</p>
                <div class="instruction">
                    <strong>To connect:</strong><br/>
                    1. Launch the official <strong>OfficeCore Workstation</strong> desktop application.<br/>
                    2. Enter your assigned server address to sign into your workspace.
                </div>
            </div>
        </body>
        </html>
        """;

    public static bool IsAuthorizedClient(IHeaderDictionary headers)
    {
        var clientHeader = headers[ClientHeaderName].ToString();
        return clientHeader.Contains(ExpectedClientToken, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldBlockRequest(HttpRequest request)
    {
        if (HttpMethods.IsOptions(request.Method))
        {
            return false;
        }

        if (IsAuthorizedClient(request.Headers))
        {
            return false;
        }

        var path = request.Path.Value ?? string.Empty;

        // Protect uploaded media and backend-served HTML/SPA routes from direct browser access
        if (path.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Allow backend API, SignalR hubs, and Blazor runtime/static assets needed by WebView2
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/attendanceHub", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var ext in AllowedAssetExtensions)
        {
            if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static async Task EnforceAsync(HttpContext context, Func<Task> next)
    {
        if (ShouldBlockRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(RestrictedHtml);
            return;
        }

        await next();
    }
}
