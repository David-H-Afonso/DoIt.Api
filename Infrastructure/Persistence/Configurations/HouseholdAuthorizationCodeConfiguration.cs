using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class HouseholdAuthorizationCodeConfiguration : IEntityTypeConfiguration<HouseholdAuthorizationCode>
{
    public void Configure(EntityTypeBuilder<HouseholdAuthorizationCode> builder)
    {
        builder.HasKey(code => code.Id);
        builder.Property(code => code.CodeHash).HasMaxLength(128).IsRequired();
        builder.Property(code => code.RedirectUri).HasMaxLength(2048).IsRequired();
        builder.Property(code => code.CodeChallenge).HasMaxLength(128).IsRequired();
        builder.HasIndex(code => code.CodeHash).IsUnique();
        builder.HasIndex(code => new { code.ConnectionId, code.ExpiresAt });
        builder.HasOne(code => code.Connection)
            .WithMany(connection => connection.AuthorizationCodes)
            .HasForeignKey(code => code.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
