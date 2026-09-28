using System.Security.Cryptography;
using System.Text;
using api.Database;
using api.Models.Common;
using api.Models.Static;
using api.Services.JwtToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using contracts.Response;
using RegisterRequest = contracts.Request.RegisterRequest;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : Controller
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<AuthController> _logger;
    private readonly IJwtTokenService _jwtTokenService; 

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    
    private readonly JwtSettings _jwtSettings;
    
    public AuthController(
        IDbContextFactory<ApplicationDbContext> dbContextFactory, 
        ILogger<AuthController> logger,
        IJwtTokenService jwtTokenService,  
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager, 
        IOptions<JwtSettings> jwtSettings)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _jwtTokenService = jwtTokenService; 
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtSettings = jwtSettings.Value;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest requests, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await _userManager.FindByEmailAsync(requests.Email);
        if (user == null) return Unauthorized();
        
        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, requests.Password, lockoutOnFailure: false);
        if (!signInResult.Succeeded)
        {
            _logger.LogError("HTTP POST /api/auth/login User {UserEmail} failed to logged in", user.Email);
            return Unauthorized();
        }
        
        var now = DateTimeOffset.UtcNow;
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtTokenService.CreateAccessToken(user.Id, user.Email ?? "", roles);
        var refreshToken = _jwtTokenService.CreateRefreshToken(); 
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenLifetimeInDays);
        
        await CreateRefreshTokenAsync(dbContext, user.Id, refreshToken, refreshTokenExpiry, cancellationToken);
        SetRefreshTokenCookie(refreshToken, refreshTokenExpiry);
        
        _logger.LogInformation("HTTP POST /api/auth/login User {UserEmail} logged in", user.Email);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = now.AddMinutes(_jwtSettings.AccessTokenLifetimeInMinutes)
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(_jwtSettings.RefreshCookieName, out var refreshToken);
        if (string.IsNullOrWhiteSpace(refreshToken)) return Unauthorized();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var tokenHash = HashRefreshToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (storedToken is not null && storedToken.RevokedAt is null)
        {
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        DeleteRefreshTokenCookie();
        return NoContent();
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = false
        };
        
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);

        var userRole = await _userManager.AddToRoleAsync(user, "User");
        if (!userRole.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return BadRequest(userRole.Errors);
        }

        return Ok();
    }
    
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(_jwtSettings.RefreshCookieName, out var refreshToken);
        if (string.IsNullOrWhiteSpace(refreshToken)) return Unauthorized();

        var tokenHash = HashRefreshToken(refreshToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(x => x.ApplicationUser)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (storedToken == null) return Unauthorized();
        if (storedToken.RevokedAt.HasValue || storedToken.ExpiresAt <= DateTimeOffset.UtcNow) return Unauthorized();

        var user = storedToken.ApplicationUser;
        if (user == null) return Unauthorized();

        var now = DateTimeOffset.UtcNow;
        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _jwtTokenService.CreateAccessToken(user.Id, user.Email ?? string.Empty, roles);

        var newRefreshToken = _jwtTokenService.CreateRefreshToken();
        var newRefreshTokenExpiry = now.AddDays(_jwtSettings.RefreshTokenLifetimeInDays);

        var replacementToken = new RefreshToken
        {
            ApplicationUserId = user.Id,
            TokenHash = HashRefreshToken(newRefreshToken),
            ExpiresAt = newRefreshTokenExpiry,
            CreatedAt = now
        };

        dbContext.RefreshTokens.Add(replacementToken);

        storedToken.RevokedAt = now;
        storedToken.ReplacedByTokenId = replacementToken.Id;

        await dbContext.SaveChangesAsync(cancellationToken);

        SetRefreshTokenCookie(newRefreshToken, newRefreshTokenExpiry);

        return Ok(new AuthResponse
        {
            AccessToken = newAccessToken,
            AccessTokenExpiresAt = now.AddMinutes(_jwtSettings.AccessTokenLifetimeInMinutes)
        });
    }
    
    private static async Task CreateRefreshTokenAsync(ApplicationDbContext dbContext, Guid userId, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var token = new RefreshToken
        {
            ApplicationUserId = userId,
            TokenHash = HashRefreshToken(refreshToken),
            ExpiresAt = expiresAt
        };

        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    
    private static string HashRefreshToken(string refreshToken)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }
    
    private void SetRefreshTokenCookie(string refreshToken, DateTimeOffset expiresAt)
    {
        Response.Cookies.Append(_jwtSettings.RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = expiresAt
        });
    }
    
    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(_jwtSettings.RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/"
        });
    }
}