using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ui;
using MudBlazor.Services;
using ui.Models.Static;
using ui.Services.Auth;
using ui.Services.Handler;
using ui.Services.JwtState;

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