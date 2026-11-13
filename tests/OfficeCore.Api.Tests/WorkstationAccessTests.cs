using Microsoft.AspNetCore.Http;
using OfficeCore.Api.Services;
using Xunit;

namespace OfficeCore.Api.Tests;

public class WorkstationAccessTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/login")]
    [InlineData("/dashboard")]
    [InlineData("/myleaves")]
    [InlineData("/admin/leave-approvals")]
    [InlineData("/uploads/leaves/sample.png")]
    public async Task Direct_browser_website_requests_without_client_header_return_403(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        await WorkstationAccessGuard.EnforceAsync(context, () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var html = await reader.ReadToEndAsync();
        Assert.Contains("Official Client Required", html);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/login")]
    [InlineData("/dashboard")]
    [InlineData("/uploads/leaves/sample.png")]
    public async Task Official_desktop_workstation_header_is_allowed(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Request.Headers[WorkstationAccessGuard.ClientHeaderName] = WorkstationAccessGuard.ExpectedClientToken;

        var nextCalled = false;
        await WorkstationAccessGuard.EnforceAsync(context, () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/hubs/chat/negotiate")]
    [InlineData("/attendanceHub/negotiate")]
    [InlineData("/_framework/blazor.webassembly.js")]
    [InlineData("/app.css")]
    public async Task Api_SignalR_and_app_runtime_assets_pass_through(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;

        var nextCalled = false;
        await WorkstationAccessGuard.EnforceAsync(context, () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
    }
}
