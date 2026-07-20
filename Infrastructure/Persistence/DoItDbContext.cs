using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Infrastructure.Persistence;

public sealed class DoItDbContext(DbContextOptions<DoItDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<DoItTask> Tasks => Set<DoItTask>();
    public DbSet<TaskSchedule> TaskSchedules => Set<TaskSchedule>();
    public DbSet<TaskOccurrence> TaskOccurrences => Set<TaskOccurrence>();
    public DbSet<TaskCompletion> TaskCompletions => Set<TaskCompletion>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<UserXp> UserXp => Set<UserXp>();
    public DbSet<XpEvent> XpEvents => Set<XpEvent>();
    public DbSet<ThemePreference> ThemePreferences => Set<ThemePreference>();
    public DbSet<BackupSchedule> BackupSchedules => Set<BackupSchedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DoItDbContext).Assembly);
    }
}
