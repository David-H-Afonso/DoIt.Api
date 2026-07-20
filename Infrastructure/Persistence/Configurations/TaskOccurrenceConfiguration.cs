using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class TaskOccurrenceConfiguration : IEntityTypeConfiguration<TaskOccurrence>
{
    public void Configure(EntityTypeBuilder<TaskOccurrence> builder)
    {
        builder.HasKey(occurrence => occurrence.Id);
        builder.Property(occurrence => occurrence.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(occurrence => new { occurrence.TaskId, occurrence.Date }).IsUnique();
        builder.HasOne(occurrence => occurrence.Task)
            .WithMany(task => task.Occurrences)
            .HasForeignKey(occurrence => occurrence.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
