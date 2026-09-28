using Microsoft.AspNetCore.Components.WebAssembly.Http;
using ui.Services.JwtState;

namespace ui.Services.Handler;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly JwtAuthStateProvider _jwtAuthStateProvider;
    
    public AuthHeaderHandler(JwtAuthStateProvider jwtAuthStateProvider)
    {
        _jwtAuthStateProvider = jwtAuthStateProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) 
    {
        var token = _jwtAuthStateProvider.GetToken(); 
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        return await base.SendAsync(request, cancellationToken);
    }
}