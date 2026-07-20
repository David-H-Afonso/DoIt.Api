using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Role).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(assignment => new { assignment.TaskId, assignment.UserId }).IsUnique();
        builder.HasOne(assignment => assignment.Task)
            .WithMany(task => task.Assignments)
            .HasForeignKey(assignment => assignment.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(assignment => assignment.User)
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
