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
        var occurrence = await dbContext.TaskOccurrences
            .Include(candidate => candidate.Completions)
            .ThenInclude(completion => completion.User)
            .FirstOrDefaultAsync(candidate => candidate.TaskId == task.Id && candidate.Date == date, cancellationToken);
        if (occurrence is not null)
        {
            return occurrence;
        }

        occurrence = new TaskOccurrence
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Date = date,
            TimeZoneId = task.Schedule?.TimeZoneId,
            AvailableFromAt = Combine(date, task.Schedule?.AvailableFromTime, task.Schedule?.TimeZoneId),
            AvailableUntilAt = Combine(date, task.Schedule?.AvailableUntilTime, task.Schedule?.TimeZoneId),
            RecommendedAt = Combine(date, task.Schedule?.RecommendedTime, task.Schedule?.TimeZoneId),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.TaskOccurrences.Add(occurrence);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return occurrence;
        }
        catch (DbUpdateException)
        {
            // Another request may have materialized this task/date between our read and
            // insert. Let the unique index arbitrate and use the existing occurrence.
            dbContext.Entry(occurrence).State = EntityState.Detached;
            var existing = await dbContext.TaskOccurrences
                .Include(candidate => candidate.Completions)
                .ThenInclude(completion => completion.User)
                .FirstOrDefaultAsync(candidate => candidate.TaskId == task.Id && candidate.Date == date, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            throw;
        }
    }

    private static DateTime? Combine(DateOnly date, TimeOnly? time, string? timeZoneId) => time is null ? null : TimeZoneHelper.ToUtc(date, time, timeZoneId);
}
