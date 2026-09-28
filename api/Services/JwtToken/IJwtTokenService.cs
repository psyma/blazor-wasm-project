namespace api.Services.JwtToken;

public interface IJwtTokenService
{
    string CreateAccessToken(Guid userId, string email, IEnumerable<string> roles);
    string CreateRefreshToken();
}