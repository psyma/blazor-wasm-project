using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using api.Database;

namespace api.Models.Common;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }

    [MaxLength(128)]
    public string TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedByTokenId { get; set; }
    public RefreshToken? ReplacedByToken { get; set; }

    [NotMapped]
    public bool IsActive => RevokedAt == null && DateTimeOffset.UtcNow < ExpiresAt;
}