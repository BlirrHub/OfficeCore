using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Microsoft.AspNetCore.Components.Web;
using OfficeCore.Web.Client.Auth;

using System.Globalization;

var cultureInfo = new CultureInfo("en-PH");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<OfficeCore.Web.Client.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddTransient<OfficeCore.Web.Client.Auth.CookieHandler>();

var baseUri = new Uri(builder.HostEnvironment.BaseAddress);
var apiUrl = $"{baseUri.Scheme}://{baseUri.Host}:5026";
builder.Services.AddHttpClient("Default", client => 
    client.BaseAddress = new Uri(apiUrl))
    .AddHttpMessageHandler<OfficeCore.Web.Client.Auth.CookieHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Default"));

// Auth services
builder.Services.AddAuthorizationCore(options =>
{
    var modules = new[] { "Dashboard", "Clients", "Attendance", "Boards", "Payroll", "Invoices", "Ledger", "WorkSchedules", "TimeOff", "Locations", "Users", "Roles", "Organization", "Advances" };
    foreach (var module in modules)
    {
        options.AddPolicy($"Require{module}", policy => policy.RequireClaim("ModuleAccess", module));
    }
});
builder.Services.AddScoped<CookieAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CookieAuthStateProvider>());

await builder.Build().RunAsync();
