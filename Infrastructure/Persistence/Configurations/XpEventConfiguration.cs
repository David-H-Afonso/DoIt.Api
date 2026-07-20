using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class XpEventConfiguration : IEntityTypeConfiguration<XpEvent>
{
    public void Configure(EntityTypeBuilder<XpEvent> builder)
    {
        builder.HasKey(xpEvent => xpEvent.Id);
        builder.Property(xpEvent => xpEvent.Reason).HasMaxLength(80).IsRequired();
        builder.Property(xpEvent => xpEvent.Complexity).HasMaxLength(32).IsRequired();
        builder.Property(xpEvent => xpEvent.Importance).HasMaxLength(32).IsRequired();
        builder.HasIndex(xpEvent => new { xpEvent.UserId, xpEvent.CreatedAt });
        builder.HasIndex(xpEvent => xpEvent.CompletionId).IsUnique();
        builder.HasOne(xpEvent => xpEvent.User)
            .WithMany()
            .HasForeignKey(xpEvent => xpEvent.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(xpEvent => xpEvent.Occurrence)
            .WithMany()
            .HasForeignKey(xpEvent => xpEvent.OccurrenceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(xpEvent => xpEvent.Task)
            .WithMany()
            .HasForeignKey(xpEvent => xpEvent.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(xpEvent => xpEvent.Completion)
            .WithMany()
            .HasForeignKey(xpEvent => xpEvent.CompletionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
