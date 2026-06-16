using Microsoft.AspNetCore.Components.Authorization;
using OfficeCore.Web.Client.Auth;
using OfficeCore.Web.Components;

var builder = WebApplication.CreateBuilder(args);

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7001";

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Auth services (server-side SSR context)
builder.Services.AddAuthorizationCore();
builder.Services.AddHttpContextAccessor();

// HttpClient for server-side calls to the API (forwards cookies from user requests)
builder.Services.AddHttpClient("ApiClient", c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var client = factory.CreateClient("ApiClient");
    // Forward the auth cookie from the browser to the API
    if (httpContext?.Request.Cookies.TryGetValue("OfficeCoreAuth", out var cookie) == true)
        client.DefaultRequestHeaders.Add("Cookie", $"OfficeCoreAuth={cookie}");
    return client;
});

builder.Services.AddScoped<CookieAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CookieAuthStateProvider>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(OfficeCore.Web.Client._Imports).Assembly);

app.Run();
