using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace ui.Services.JwtState;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private string? _token;
    private ClaimsPrincipal? _user;

    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public string? GetToken() => _token;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_user ?? Anonymous));
    }

    public void NotifyUserAuthentication(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            NotifyUserLogout();
            return;
        }

        var jwt = handler.ReadJwtToken(token);
        var identity = new ClaimsIdentity(jwt.Claims, authenticationType: "jwt", nameType: JwtRegisteredClaimNames.Email, roleType: ClaimTypes.Role);

        _token = token;
        _user = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_user)));
    }

    public void NotifyUserLogout()
    {
        _user = null;
        _token = null;

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    public bool HasRole(string role)
    {
        return _user?.IsInRole(role) == true;
    }

    public string? GetUserEmail()
    {
        return _user?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
    }

    public Guid? GetUserId()
    {
        var value = _user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}