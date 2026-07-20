using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class UserXpConfiguration : IEntityTypeConfiguration<UserXp>
{
    public void Configure(EntityTypeBuilder<UserXp> builder)
    {
        builder.HasKey(userXp => userXp.Id);
        builder.HasIndex(userXp => userXp.UserId).IsUnique();
        builder.HasOne(userXp => userXp.User)
            .WithOne()
            .HasForeignKey<UserXp>(userXp => userXp.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
