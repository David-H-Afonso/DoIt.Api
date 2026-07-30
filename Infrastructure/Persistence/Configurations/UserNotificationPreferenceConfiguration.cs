using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.ToTable("UserNotificationPreferences");
        builder.HasKey(preference => preference.UserId);
        builder.Property(preference => preference.AvailableFromEnabled).HasDefaultValue(true);
        builder.Property(preference => preference.RecommendedEnabled).HasDefaultValue(true);
        builder.Property(preference => preference.BeforeAvailableUntilEnabled).HasDefaultValue(true);
        builder.Property(preference => preference.TaskExpiredEnabled).HasDefaultValue(true);
        builder.Property(preference => preference.TaskCompletedEnabled).HasDefaultValue(true);
        builder.Property(preference => preference.BeforeAvailableUntilMinutes).HasDefaultValue(30);
        builder.HasOne(preference => preference.User)
            .WithOne(user => user.NotificationPreference)
            .HasForeignKey<UserNotificationPreference>(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
