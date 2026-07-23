using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class HouseholdAccessTokenConfiguration : IEntityTypeConfiguration<HouseholdAccessToken>
{
    public void Configure(EntityTypeBuilder<HouseholdAccessToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(token => token.JwtId).HasMaxLength(64).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.JwtId).IsUnique();
        builder.HasIndex(token => new { token.ConnectionId, token.FamilyId, token.ExpiresAt });
        builder.HasOne(token => token.Connection)
            .WithMany(connection => connection.AccessTokens)
            .HasForeignKey(token => token.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
