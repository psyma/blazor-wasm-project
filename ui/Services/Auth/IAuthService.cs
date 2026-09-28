using contracts.Request;

namespace ui.Services.Auth;

public interface IAuthService
{
    Task<bool> Login(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Tuple<bool, string>> Register(RegisterRequest request, CancellationToken cancellationToken = default);
    Task Logout(string? email, CancellationToken cancellationToken = default);
    Task<bool> Refresh(CancellationToken cancellationToken = default);
    
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<bool> Me();
}