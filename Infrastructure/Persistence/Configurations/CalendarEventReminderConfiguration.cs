using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class CalendarEventReminderConfiguration : IEntityTypeConfiguration<CalendarEventReminder>
{
    public void Configure(EntityTypeBuilder<CalendarEventReminder> builder)
    {
        builder.ToTable("CalendarEventReminders");
        builder.HasKey(reminder => reminder.Id);
        builder.HasIndex(reminder => new { reminder.CalendarEventId, reminder.OffsetMinutes }).IsUnique();
        builder.HasIndex(reminder => new { reminder.CalendarEventId, reminder.IsEnabled });
    }
}
