using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Username).HasMaxLength(80).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(user => user.PreferredLocale).HasMaxLength(12).IsRequired();
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(32).HasDefaultValue(UserRole.User);
        builder.HasIndex(user => user.Username).IsUnique();
        builder.HasMany(user => user.RefreshTokens)
            .WithOne(token => token.User)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(user => user.BackupSchedules)
            .WithOne(schedule => schedule.User)
            .HasForeignKey(schedule => schedule.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
