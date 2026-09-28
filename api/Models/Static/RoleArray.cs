using Microsoft.AspNetCore.Identity;

namespace api.Models.Static;

public class RoleArray
{
    public List<IdentityRole<Guid>> Roles { get; set; } = new();
}