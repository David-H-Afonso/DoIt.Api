using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class HouseholdConnectionConfiguration : IEntityTypeConfiguration<HouseholdConnection>
{
    public void Configure(EntityTypeBuilder<HouseholdConnection> builder)
    {
        builder.HasKey(connection => connection.Id);
        builder.Property(connection => connection.ClientId).HasMaxLength(80).IsRequired();
        builder.Property(connection => connection.GrantedScopes).HasMaxLength(512).IsRequired();
        builder.Property(connection => connection.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(connection => new { connection.UserId, connection.ClientId, connection.Status });
        builder.HasOne(connection => connection.User)
            .WithMany(user => user.HouseholdConnections)
            .HasForeignKey(connection => connection.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
