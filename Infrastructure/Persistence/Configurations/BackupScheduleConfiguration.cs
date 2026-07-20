using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class BackupScheduleConfiguration : IEntityTypeConfiguration<BackupSchedule>
{
    public void Configure(EntityTypeBuilder<BackupSchedule> builder)
    {
        builder.HasKey(schedule => schedule.Id);
        builder.Property(schedule => schedule.DestinationPath).HasMaxLength(500).IsRequired();
        builder.Property(schedule => schedule.FileNamePrefix).HasMaxLength(80).IsRequired();
        builder.Property(schedule => schedule.FileNameSuffix).HasMaxLength(80).IsRequired();
        builder.Property(schedule => schedule.LastRunStatus).HasMaxLength(24).IsRequired();
        builder.Property(schedule => schedule.LastRunMessage).HasMaxLength(1000);
        builder.HasIndex(schedule => schedule.UserId).IsUnique();
        builder.HasOne(schedule => schedule.User)
            .WithMany(user => user.BackupSchedules)
            .HasForeignKey(schedule => schedule.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
