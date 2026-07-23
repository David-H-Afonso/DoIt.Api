using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class HouseholdRefreshTokenConfiguration : IEntityTypeConfiguration<HouseholdRefreshToken>
{
    public void Configure(EntityTypeBuilder<HouseholdRefreshToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.ConnectionId, token.FamilyId, token.ExpiresAt });
        builder.HasOne(token => token.Connection)
            .WithMany(connection => connection.RefreshTokens)
            .HasForeignKey(token => token.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
