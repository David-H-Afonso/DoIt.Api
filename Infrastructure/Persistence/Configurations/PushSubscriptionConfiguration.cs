using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("PushSubscriptions");
        builder.HasKey(subscription => subscription.Id);
        builder.Property(subscription => subscription.Endpoint).HasMaxLength(2048).IsRequired();
        builder.Property(subscription => subscription.P256dh).HasMaxLength(512).IsRequired();
        builder.Property(subscription => subscription.Auth).HasMaxLength(512).IsRequired();
        builder.Property(subscription => subscription.DeviceName).HasMaxLength(200);
        builder.HasIndex(subscription => subscription.Endpoint).IsUnique();
        builder.HasIndex(subscription => new { subscription.UserId, subscription.IsActive });
        builder.HasOne(subscription => subscription.User)
            .WithMany(user => user.PushSubscriptions)
            .HasForeignKey(subscription => subscription.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(subscription => subscription.NotificationDeliveries)
            .WithOne(delivery => delivery.PushSubscription)
            .HasForeignKey(delivery => delivery.PushSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
