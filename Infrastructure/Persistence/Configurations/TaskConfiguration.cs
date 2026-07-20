using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class TaskConfiguration : IEntityTypeConfiguration<DoItTask>
{
    public void Configure(EntityTypeBuilder<DoItTask> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(task => task.Id);
        builder.Property(task => task.Title).HasMaxLength(220).IsRequired();
        builder.Property(task => task.Description).HasMaxLength(2000);
        builder.Property(task => task.Scope).HasConversion<string>().HasMaxLength(32);
        builder.Property(task => task.TaskType).HasConversion<string>().HasMaxLength(32);
        builder.Property(task => task.Importance).HasConversion<string>().HasMaxLength(32);
        builder.Property(task => task.Complexity).HasConversion<string>().HasMaxLength(32);
        builder.Property(task => task.Obligation).HasConversion<string>().HasMaxLength(32);
        builder.Property(task => task.AssignmentMode).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(task => new { task.CreatedByUserId, task.IsArchived });
        builder.HasOne(task => task.CreatedByUser)
            .WithMany()
            .HasForeignKey(task => task.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(task => task.Zone)
            .WithMany(zone => zone.Tasks)
            .HasForeignKey(task => task.ZoneId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(task => task.Schedule)
            .WithOne(schedule => schedule.Task)
            .HasForeignKey<TaskSchedule>(schedule => schedule.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
