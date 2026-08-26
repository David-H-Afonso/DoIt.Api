using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class NotificationInboxItemConfiguration : IEntityTypeConfiguration<NotificationInboxItem>
{
    public void Configure(EntityTypeBuilder<NotificationInboxItem> builder)
    {
        builder.ToTable("NotificationInboxItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SourceType).HasMaxLength(64).IsRequired();
        builder.Property(item => item.DeduplicationKey).HasMaxLength(256).IsRequired();
        builder.Property(item => item.Title).HasMaxLength(220).IsRequired();
        builder.Property(item => item.Body).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.Url).HasMaxLength(512).IsRequired();
        builder.Property(item => item.DataJson).HasMaxLength(4000);
        builder.HasIndex(item => new { item.UserId, item.DeduplicationKey }).IsUnique();
        builder.HasIndex(item => new { item.UserId, item.ReadAtUtc, item.CreatedAt });
        builder.HasIndex(item => new { item.SourceType, item.SourceId });
        builder.HasOne(item => item.User)
            .WithMany(user => user.NotificationInboxItems)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
