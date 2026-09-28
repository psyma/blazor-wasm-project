using System.Net.Http.Json;
using contracts.Request;
using contracts.Response;
using ui.Services.JwtState;

namespace ui.Services.Auth;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly JwtAuthStateProvider _authStateProvider;
    
    private bool IsInitialized { get; set; }
    
    public AuthService(HttpClient httpClient, JwtAuthStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _authStateProvider = authStateProvider;
    }
    
    public async Task<bool> Login(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode) return false;
         
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
        if (authResponse == null) return false;

        _authStateProvider.NotifyUserAuthentication(authResponse.AccessToken);
        IsInitialized = true;
        return true;
    }

    public async Task<Tuple<bool, string>> Register(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request, cancellationToken: cancellationToken);
        var success = response.IsSuccessStatusCode;
        var message =  await response.Content.ReadAsStringAsync(cancellationToken);
        
        return new Tuple<bool, string>(success, message);
    }

    public async Task Logout(string? email, CancellationToken cancellationToken = default)
    {
        await _httpClient.PostAsync($"/api/auth/logout?email={email}", null, cancellationToken);
        
        _authStateProvider.NotifyUserLogout();
        IsInitialized = true;
    }

    public async Task<bool> Refresh(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh", new {}, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode) return false;
        
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
        if (authResponse == null) return false;
        
        _authStateProvider.NotifyUserAuthentication(authResponse.AccessToken);
        return true;
    }
    
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized) return;

        var success = await Refresh(cancellationToken);
        if (!success) _authStateProvider.NotifyUserLogout();

        IsInitialized = true;
    }

    public async Task<bool> Me()
    {
        var response = await _httpClient.GetAsync("/api/test/me");
        return response.IsSuccessStatusCode;
    }
}