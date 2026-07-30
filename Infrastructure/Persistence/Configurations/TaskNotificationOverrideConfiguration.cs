using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class TaskNotificationOverrideConfiguration : IEntityTypeConfiguration<TaskNotificationOverride>
{
    public void Configure(EntityTypeBuilder<TaskNotificationOverride> builder)
    {
        builder.ToTable("TaskNotificationOverrides");
        builder.HasKey(notificationOverride => notificationOverride.TaskId);
        builder.HasOne(notificationOverride => notificationOverride.Task)
            .WithOne(task => task.NotificationOverride)
            .HasForeignKey<TaskNotificationOverride>(notificationOverride => notificationOverride.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
