using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class OccurrenceService(DoItDbContext dbContext) : IOccurrenceService
{
    public async Task<TaskOccurrence> GetOrCreateAsync(DoItTask task, DateOnly date, DateTime now, CancellationToken cancellationToken)
    {
        var occurrence = await dbContext.TaskOccurrences.FirstOrDefaultAsync(candidate => candidate.TaskId == task.Id && candidate.Date == date, cancellationToken);
        if (occurrence is not null)
        {
            return occurrence;
        }

        occurrence = new TaskOccurrence
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Date = date,
            AvailableFromAt = Combine(date, task.Schedule?.AvailableFromTime, task.Schedule?.TimeZoneId),
            AvailableUntilAt = Combine(date, task.Schedule?.AvailableUntilTime, task.Schedule?.TimeZoneId),
            RecommendedAt = Combine(date, task.Schedule?.RecommendedTime, task.Schedule?.TimeZoneId),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.TaskOccurrences.Add(occurrence);
        await dbContext.SaveChangesAsync(cancellationToken);
        return occurrence;
    }

    private static DateTime? Combine(DateOnly date, TimeOnly? time, string? timeZoneId) => time is null ? null : TimeZoneHelper.ToUtc(date, time, timeZoneId);
}
