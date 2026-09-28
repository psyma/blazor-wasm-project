using api.Database;
using api.Models.Common;
using api.Models.Static;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
         
        if (await context.DataSeedStates.AnyAsync()) return;

        await context.Database.MigrateAsync();
        
        var roleArray = provider.GetRequiredService<IOptions<RoleArray>>().Value;
        
        await context.Database.OpenConnectionAsync();
        try
        {
            await context.Roles.AddRangeAsync(roleArray.Roles);
            await context.DataSeedStates.AddAsync(new DataSeedState { CreatedAt = DateTime.UtcNow });
        
            await context.SaveChangesAsync();
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}