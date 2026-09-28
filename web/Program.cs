using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using web;
using MudBlazor.Services;
using web.Models.Static;
using web.Services.Auth;
using web.Services.Handler;
using web.Services.JwtState;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddSingleton<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddScoped<AuthHeaderHandler>();
builder.Services.AddScoped<RetryOnUnauthorizedHandler>(); 

var apiSettings = builder.Configuration.GetSection("ApiSettings").Get<ApiSettings>() ?? throw new InvalidOperationException("Api settings not found.");
builder.Services.AddHttpClient("RefreshClient", o => { o.BaseAddress = new Uri(apiSettings.BaseUrl); }).RemoveAllLoggers();
builder.Services.AddHttpClient<IAuthService, AuthService>(o => { o.BaseAddress = new Uri(apiSettings.BaseUrl); })
    .AddHttpMessageHandler<RetryOnUnauthorizedHandler>()
    .AddHttpMessageHandler<AuthHeaderHandler>().RemoveAllLoggers();

await builder.Build().RunAsync();