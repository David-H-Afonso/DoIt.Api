using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class ThemePreferenceConfiguration : IEntityTypeConfiguration<ThemePreference>
{
    public void Configure(EntityTypeBuilder<ThemePreference> builder)
    {
        builder.HasKey(theme => theme.Id);
        builder.Property(theme => theme.ThemeMode).HasMaxLength(24).IsRequired();
        builder.Property(theme => theme.PrimaryColor).HasMaxLength(16).IsRequired();
        builder.Property(theme => theme.AccentColor).HasMaxLength(16).IsRequired();
        builder.Property(theme => theme.BackgroundColor).HasMaxLength(16).IsRequired();
        builder.Property(theme => theme.SurfaceColor).HasMaxLength(16).IsRequired();
        builder.Property(theme => theme.TextColor).HasMaxLength(16);
        builder.Property(theme => theme.BackgroundImagePath).HasMaxLength(1000);
        builder.Property(theme => theme.BackgroundOverlayColor).HasMaxLength(16).IsRequired();
        builder.HasIndex(theme => theme.UserId).IsUnique();
        builder.HasOne(theme => theme.User)
            .WithOne()
            .HasForeignKey<ThemePreference>(theme => theme.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
