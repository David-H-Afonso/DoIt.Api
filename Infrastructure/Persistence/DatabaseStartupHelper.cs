using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Infrastructure.Persistence;

public static class DatabaseStartupHelper
{
    public static async Task ApplyMigrationsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DoItDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
