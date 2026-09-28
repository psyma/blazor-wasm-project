using System.ComponentModel.DataAnnotations;
using api.Models.Common;
using Microsoft.AspNetCore.Identity;

namespace api.Database;

public class ApplicationUser : IdentityUser<Guid>
{
    [MaxLength(64)]
    public string? FirstName { get; set; }
    [MaxLength(64)]
    public string? LastName { get; set; }
    
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}