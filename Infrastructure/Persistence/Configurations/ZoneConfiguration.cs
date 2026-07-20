using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.HasKey(zone => zone.Id);
        builder.Property(zone => zone.Name).HasMaxLength(120).IsRequired();
        builder.Property(zone => zone.Description).HasMaxLength(1000);
        builder.Property(zone => zone.Color).HasMaxLength(32);
        builder.Property(zone => zone.Icon).HasMaxLength(80);
        builder.HasIndex(zone => new { zone.CreatedByUserId, zone.SortOrder });
        builder.HasOne(zone => zone.CreatedByUser)
            .WithMany()
            .HasForeignKey(zone => zone.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
