namespace api.Models.Static;

public class JwtSettings
{
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string Secret { get; set; }
    public required string RefreshCookieName { get; set; }
    public required int AccessTokenLifetimeInMinutes { get; set; }
    public required int RefreshTokenLifetimeInDays { get; set; }
}