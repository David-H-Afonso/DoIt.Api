using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class TaskOccurrenceSnoozeConfiguration : IEntityTypeConfiguration<TaskOccurrenceSnooze>
{
    public void Configure(EntityTypeBuilder<TaskOccurrenceSnooze> builder)
    {
        builder.HasKey(snooze => snooze.Id);
        builder.HasIndex(snooze => new { snooze.OccurrenceId, snooze.UserId }).IsUnique();
        builder.HasIndex(snooze => new { snooze.UserId, snooze.UntilAtUtc });
        builder.HasOne(snooze => snooze.Occurrence)
            .WithMany()
            .HasForeignKey(snooze => snooze.OccurrenceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(snooze => snooze.User)
            .WithMany()
            .HasForeignKey(snooze => snooze.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
