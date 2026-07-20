using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class TaskCompletionConfiguration : IEntityTypeConfiguration<TaskCompletion>
{
    public void Configure(EntityTypeBuilder<TaskCompletion> builder)
    {
        builder.HasKey(completion => completion.Id);
        builder.Property(completion => completion.Action).HasConversion<string>().HasMaxLength(32);
        builder.Property(completion => completion.Notes).HasMaxLength(1000);
        builder.HasIndex(completion => new { completion.OccurrenceId, completion.RevertedAt });
        builder.HasOne(completion => completion.Occurrence)
            .WithMany(occurrence => occurrence.Completions)
            .HasForeignKey(completion => completion.OccurrenceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(completion => completion.User)
            .WithMany()
            .HasForeignKey(completion => completion.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
