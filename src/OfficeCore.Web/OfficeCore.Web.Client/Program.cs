using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Microsoft.AspNetCore.Components.Web;
using OfficeCore.Web.Client.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<OfficeCore.Web.Client.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddTransient<OfficeCore.Web.Client.Auth.CookieHandler>();

builder.Services.AddHttpClient("Default", client => 
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7001"))
    .AddHttpMessageHandler<OfficeCore.Web.Client.Auth.CookieHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Default"));

// Auth services
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CookieAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CookieAuthStateProvider>());

await builder.Build().RunAsync();
