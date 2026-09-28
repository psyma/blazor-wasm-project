using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using contracts.Response;
using Microsoft.AspNetCore.Components;
using web.Services.JwtState;

namespace web.Services.Handler;

public class RetryOnUnauthorizedHandler : DelegatingHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NavigationManager _navigationManager;
    private readonly JwtAuthStateProvider _jwtAuthStateProvider;
    private static readonly HttpRequestOptionsKey<bool> RetryAttemptedKey = new("RetryAttempted");

    public RetryOnUnauthorizedHandler(
        IHttpClientFactory httpClientFactory,
        NavigationManager navigationManager,
        JwtAuthStateProvider jwtAuthStateProvider)
    {
        _httpClientFactory = httpClientFactory;
        _navigationManager = navigationManager;
        _jwtAuthStateProvider = jwtAuthStateProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // First request
        var response = await base.SendAsync(request, cancellationToken);

        // Not 401 => done
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // Already retried once => stop
        if (request.Options.TryGetValue(RetryAttemptedKey, out var retried) && retried)
            return response;

        response.Dispose();

        // IMPORTANT: use a plain client with NO retry/auth handlers
        var client = _httpClientFactory.CreateClient("RefreshClient");
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new { }, cancellationToken);

        if (!refreshResponse.IsSuccessStatusCode)
        {
            try
            {
                var authState = await _jwtAuthStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;
                var email = user.Identity?.Name;

                if (!string.IsNullOrWhiteSpace(email))
                {
                    await client.PostAsync(
                        $"/api/auth/logout?email={Uri.EscapeDataString(email)}",
                        null,
                        cancellationToken);
                }
            }
            catch
            {
                //
            }

            var authState2 = await _jwtAuthStateProvider.GetAuthenticationStateAsync();
            var wasAuthenticated = authState2.User.Identity?.IsAuthenticated == true;

            _jwtAuthStateProvider.NotifyUserLogout();

            if (wasAuthenticated && !IsInCurrentUrlPath("/login"))
            {
                _navigationManager.NavigateTo("/login", forceLoad: true);
            }

            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        await using var contentStream = await refreshResponse.Content.ReadAsStreamAsync(cancellationToken);
        var authResponse = await JsonSerializer.DeserializeAsync<AuthResponse>(
            contentStream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken
        ); 
    
        // Update auth state/UI 
        if (authResponse != null) _jwtAuthStateProvider.NotifyUserAuthentication(authResponse.AccessToken);

        // Clone original request
        var clonedRequest = await CloneHttpRequestMessageAsync(request);
        clonedRequest.Options.Set(RetryAttemptedKey, true);

        // Retry - JwtAuthorizationHandler should attach the NEW token
        return await base.SendAsync(clonedRequest, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content != null)
        {
            var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms, CancellationToken.None);
            ms.Position = 0;

            var newContent = new StreamContent(ms);

            foreach (var header in request.Content.Headers)
            {
                newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = newContent;
        }

        foreach (var option in request.Options)
        {
            // Avoid copying RetryAttempted from original request if present
            if (option.Key != RetryAttemptedKey.Key)
            {
                clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
            }
        }

        return clone;
    }

    private bool IsInCurrentUrlPath(string urlPath)
    {
        var path = "/" + _navigationManager.ToBaseRelativePath(_navigationManager.Uri)
            .Trim('/')
            .ToLower();

        return path == urlPath.Trim('/').ToLower();
    }
}