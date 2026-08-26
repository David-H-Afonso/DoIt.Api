using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries");
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.SourceType).HasMaxLength(64).IsRequired();
        builder.Property(delivery => delivery.DeduplicationKey).HasMaxLength(256).IsRequired();
        builder.Property(delivery => delivery.PushGroupKey).HasMaxLength(160);
        builder.Property(delivery => delivery.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(delivery => delivery.LastError).HasMaxLength(2000);
        builder.HasIndex(delivery => new { delivery.PushSubscriptionId, delivery.DeduplicationKey }).IsUnique();
        builder.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAtUtc, delivery.LeaseUntilUtc });
        builder.HasIndex(delivery => new { delivery.UserId, delivery.DueAtUtc });
        builder.HasIndex(delivery => new { delivery.SourceType, delivery.SourceId });
        builder.HasIndex(delivery => delivery.NotificationInboxItemId);
        builder.HasIndex(delivery => new { delivery.PushSubscriptionId, delivery.PushGroupKey })
            .IsUnique()
            .HasFilter("PushGroupKey IS NOT NULL");
        builder.HasOne(delivery => delivery.User)
            .WithMany(user => user.NotificationDeliveries)
            .HasForeignKey(delivery => delivery.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(delivery => delivery.PushSubscription)
            .WithMany(subscription => subscription.NotificationDeliveries)
            .HasForeignKey(delivery => delivery.PushSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(delivery => delivery.NotificationInboxItem)
            .WithMany(item => item.Deliveries)
            .HasForeignKey(delivery => delivery.NotificationInboxItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
