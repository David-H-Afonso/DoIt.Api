using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class TaskScheduleConfiguration : IEntityTypeConfiguration<TaskSchedule>
{
    public void Configure(EntityTypeBuilder<TaskSchedule> builder)
    {
        builder.HasKey(schedule => schedule.Id);
        builder.Property(schedule => schedule.RecurrenceType).HasConversion<string>().HasMaxLength(32);
        builder.Property(schedule => schedule.UnavailableVisibilityMode).HasConversion<string>().HasMaxLength(32);
        builder.Property(schedule => schedule.TimeZoneId).HasMaxLength(128).IsRequired();
        builder.HasIndex(schedule => schedule.TaskId).IsUnique();
    }
}
