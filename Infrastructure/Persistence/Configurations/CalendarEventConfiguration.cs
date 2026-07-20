using DoIt.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("CalendarEvents");
        builder.HasKey(calendarEvent => calendarEvent.Id);
        builder.Property(calendarEvent => calendarEvent.Title).HasMaxLength(220).IsRequired();
        builder.Property(calendarEvent => calendarEvent.Description).HasMaxLength(2000);
        builder.Property(calendarEvent => calendarEvent.TimeZoneId).HasMaxLength(128).IsRequired();
        builder.HasIndex(calendarEvent => new { calendarEvent.CreatedByUserId, calendarEvent.StartAtUtc });
        builder.HasIndex(calendarEvent => new { calendarEvent.ZoneId, calendarEvent.StartAtUtc });
        builder.HasOne(calendarEvent => calendarEvent.CreatedByUser)
            .WithMany()
            .HasForeignKey(calendarEvent => calendarEvent.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(calendarEvent => calendarEvent.Zone)
            .WithMany()
            .HasForeignKey(calendarEvent => calendarEvent.ZoneId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(calendarEvent => calendarEvent.Reminders)
            .WithOne(reminder => reminder.CalendarEvent)
            .HasForeignKey(reminder => reminder.CalendarEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
