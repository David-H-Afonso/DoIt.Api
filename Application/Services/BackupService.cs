using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class BackupService(DoItDbContext dbContext, ILogger<BackupService> logger) : IBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<IReadOnlyList<BackupScheduleResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.OrderBy(user => user.Username).ToListAsync(cancellationToken);
        var schedules = await dbContext.BackupSchedules.ToListAsync(cancellationToken);
        var scheduleMap = schedules.ToDictionary(schedule => schedule.UserId);
        return users.Select(user => ToResponse(user, scheduleMap.GetValueOrDefault(user.Id) ?? CreateDefaultSchedule(user.Id))).ToList();
    }

    public async Task<BackupScheduleResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);
        var schedule = await GetOrCreateScheduleAsync(userId, cancellationToken);
        return ToResponse(user, schedule);
    }

    public async Task<BackupScheduleResponse> UpdateAsync(Guid userId, UpdateBackupScheduleRequest request, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);
        var schedule = await GetOrCreateScheduleAsync(userId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.DestinationPath))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "backup_path_required", "Backup destination path is required.");
        }

        if (request.RetentionCount < 0)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_retention", "Retention count must be zero or greater.");
        }

        schedule.DestinationPath = request.DestinationPath.Trim();
        schedule.RetentionCount = request.RetentionCount;
        schedule.FileNamePrefix = SanitizeFilePart(request.FileNamePrefix ?? "");
        schedule.FileNameSuffix = SanitizeFilePart(request.FileNameSuffix ?? "");
        schedule.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(user, schedule);
    }

    public async Task<BackupScheduleResponse> RunNowAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);
        var schedule = await GetOrCreateScheduleAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;
        schedule.LastRunAt = now;
        schedule.LastRunStatus = "running";
        schedule.LastRunMessage = null;
        schedule.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var destination = Path.GetFullPath(schedule.DestinationPath, AppContext.BaseDirectory);
            Directory.CreateDirectory(destination);

            var backup = await BuildBackupAsync(user, now, cancellationToken);
            var fileName = BuildFileName(user, schedule, now);
            var filePath = Path.Combine(destination, fileName);
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(backup, JsonOptions), cancellationToken);

            ApplyRetention(destination, schedule.RetentionCount);
            schedule.LastRunStatus = "success";
            schedule.LastRunMessage = $"Wrote {fileName}";
            logger.LogInformation("Manual backup written for user {UserId} to {FilePath}", user.Id, filePath);
        }
        catch (Exception ex)
        {
            schedule.LastRunStatus = "failed";
            schedule.LastRunMessage = ex.Message;
            logger.LogError(ex, "Manual backup failed for user {UserId}", user.Id);
        }
        finally
        {
            schedule.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(user, schedule);
    }

    public async Task<FullBackupResponse> RunFullNowAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);
        var schedule = await GetOrCreateScheduleAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;
        var destination = Path.GetFullPath(schedule.DestinationPath, AppContext.BaseDirectory);
        Directory.CreateDirectory(destination);
        var fileName = $"doit-full-{now:yyyyMMdd-HHmmss}.sqlite";
        var filePath = Path.Combine(destination, fileName);

        try
        {
            if (dbContext.Database.GetDbConnection() is not SqliteConnection source)
            {
                throw new InvalidOperationException("The configured database provider does not support SQLite backups.");
            }

            await using var target = new SqliteConnection($"Data Source={filePath};Pooling=False");
            if (source.State != System.Data.ConnectionState.Open)
            {
                await source.OpenAsync(cancellationToken);
            }
            await target.OpenAsync(cancellationToken);
            source.BackupDatabase(target);
            target.Close();
            target.Dispose();
            SqliteConnection.ClearAllPools();

            ApplyRetention(destination, schedule.RetentionCount);
            schedule.LastRunAt = now;
            schedule.LastRunStatus = "success";
            schedule.LastRunMessage = $"Wrote {fileName}";
            schedule.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Full database backup written for user {UserId} to {FilePath}", user.Id, filePath);
            return new FullBackupResponse(fileName, new FileInfo(filePath).Length, now, destination);
        }
        catch (Exception ex)
        {
            schedule.LastRunAt = now;
            schedule.LastRunStatus = "failed";
            schedule.LastRunMessage = ex.Message;
            schedule.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogError(ex, "Full database backup failed for user {UserId}", user.Id);
            throw;
        }
    }

    private async Task<object> BuildBackupAsync(User user, DateTime exportedAt, CancellationToken cancellationToken)
    {
        var zones = await dbContext.Zones.Where(zone => zone.CreatedByUserId == user.Id).OrderBy(zone => zone.SortOrder).ToListAsync(cancellationToken);
        var tasks = await dbContext.Tasks
            .Where(task => task.CreatedByUserId == user.Id || task.Assignments.Any(assignment => assignment.UserId == user.Id))
            .Include(task => task.Schedule)
            .Include(task => task.Assignments)
            .OrderBy(task => task.CreatedAt)
            .ToListAsync(cancellationToken);
        var taskIds = tasks.Select(task => task.Id).ToHashSet();
        var occurrences = await dbContext.TaskOccurrences.Where(occurrence => taskIds.Contains(occurrence.TaskId)).OrderBy(occurrence => occurrence.Date).ToListAsync(cancellationToken);
        var occurrenceIds = occurrences.Select(occurrence => occurrence.Id).ToHashSet();
        var completions = await dbContext.TaskCompletions.Where(completion => occurrenceIds.Contains(completion.OccurrenceId) && completion.UserId == user.Id).OrderBy(completion => completion.CreatedAt).ToListAsync(cancellationToken);
        var xp = await dbContext.UserXp.FirstOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);
        var xpEvents = await dbContext.XpEvents.Where(xpEvent => xpEvent.UserId == user.Id).OrderBy(xpEvent => xpEvent.CreatedAt).ToListAsync(cancellationToken);
        var theme = await dbContext.ThemePreferences.FirstOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);
        var calendarEvents = await dbContext.CalendarEvents
            .Include(calendarEvent => calendarEvent.Reminders)
            .Where(calendarEvent => calendarEvent.CreatedByUserId == user.Id)
            .OrderBy(calendarEvent => calendarEvent.StartAtUtc)
            .ToListAsync(cancellationToken);

        return new
        {
            schema = "doit-user-backup-v1",
            exportedAt,
            user = new { user.Id, user.Username, user.DisplayName, Role = user.Role.ToString(), user.PreferredLocale, user.IsActive, user.CreatedAt, user.UpdatedAt, user.LastLoginAt },
            zones = zones.Select(zone => new { zone.Id, zone.Name, zone.Description, zone.Color, zone.Icon, zone.SortOrder, zone.IsArchived, zone.CreatedByUserId, zone.CreatedAt, zone.UpdatedAt }),
            tasks = tasks.Select(task => new
            {
                task.Id,
                task.Title,
                task.Description,
                task.ZoneId,
                Scope = task.Scope.ToString(),
                TaskType = task.TaskType.ToString(),
                Importance = task.Importance.ToString(),
                Complexity = task.Complexity.ToString(),
                Obligation = task.Obligation.ToString(),
                AssignmentMode = task.AssignmentMode.ToString(),
                task.IsArchived,
                task.CreatedByUserId,
                task.CreatedAt,
                task.UpdatedAt,
                schedule = task.Schedule is null ? null : new
                {
                    task.Schedule.Id,
                    task.Schedule.TaskId,
                    RecurrenceType = task.Schedule.RecurrenceType.ToString(),
                    task.Schedule.StartDate,
                    task.Schedule.EndDate,
                    Weekday = task.Schedule.Weekday?.ToString(),
                    task.Schedule.WeekOfMonth,
                    task.Schedule.TimesPerWeek,
                    task.Schedule.EveryNDays,
                    task.Schedule.Interval,
                    task.Schedule.AvailableFromTime,
                    task.Schedule.AvailableUntilTime,
                    task.Schedule.RecommendedTime,
                    task.Schedule.TimeZoneId,
                    UnavailableVisibilityMode = task.Schedule.UnavailableVisibilityMode.ToString(),
                    task.Schedule.CreatedAt,
                    task.Schedule.UpdatedAt
                },
                assignments = task.Assignments.Select(assignment => new { assignment.Id, assignment.TaskId, assignment.UserId, Role = assignment.Role.ToString(), assignment.CreatedAt })
            }),
            occurrences = occurrences.Select(occurrence => new { occurrence.Id, occurrence.TaskId, occurrence.Date, occurrence.TimeZoneId, Status = occurrence.Status.ToString(), occurrence.AvailableFromAt, occurrence.AvailableUntilAt, occurrence.RecommendedAt, occurrence.CreatedAt, occurrence.UpdatedAt }),
            completions = completions.Select(completion => new { completion.Id, completion.OccurrenceId, completion.UserId, Action = completion.Action.ToString(), completion.Notes, completion.CreatedAt, completion.RevertedAt }),
            xp = xp is null ? null : new { xp.Id, xp.UserId, xp.TotalXp, xp.WeeklyXp, xp.CurrentLevel, xp.UpdatedAt },
            xpEvents = xpEvents.Select(xpEvent => new { xpEvent.Id, xpEvent.UserId, xpEvent.OccurrenceId, xpEvent.TaskId, xpEvent.CompletionId, xpEvent.Amount, xpEvent.Reason, xpEvent.Complexity, xpEvent.Importance, xpEvent.FormulaVersion, xpEvent.CreatedAt, xpEvent.RevertedAt }),
            theme = theme is null ? null : new { theme.Id, theme.UserId, theme.ThemeMode, theme.PrimaryColor, theme.AccentColor, theme.BackgroundColor, theme.SurfaceColor, theme.TextColor, theme.BackgroundImagePath, theme.BackgroundOverlayColor, theme.BackgroundOverlayOpacity, theme.CreatedAt, theme.UpdatedAt },
            calendarEvents = calendarEvents.Select(calendarEvent => new
            {
                calendarEvent.Id,
                calendarEvent.Title,
                calendarEvent.Description,
                calendarEvent.ZoneId,
                calendarEvent.StartAtUtc,
                calendarEvent.EndAtUtc,
                calendarEvent.IsAllDay,
                calendarEvent.TimeZoneId,
                calendarEvent.IsCancelled,
                calendarEvent.CreatedAt,
                calendarEvent.UpdatedAt,
                reminders = calendarEvent.Reminders.Select(reminder => new { reminder.Id, reminder.OffsetMinutes, reminder.IsEnabled, reminder.AcknowledgedAt, reminder.CreatedAt, reminder.UpdatedAt })
            })
        };
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        return user ?? throw new ApiException(StatusCodes.Status404NotFound, "user_not_found", "User not found.");
    }

    private async Task<BackupSchedule> GetOrCreateScheduleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var schedule = await dbContext.BackupSchedules.FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
        if (schedule is not null)
        {
            return schedule;
        }

        schedule = CreateDefaultSchedule(userId);
        dbContext.BackupSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return schedule;
    }

    private static BackupSchedule CreateDefaultSchedule(Guid userId)
    {
        var now = DateTime.UtcNow;
        return new BackupSchedule
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static BackupScheduleResponse ToResponse(User user, BackupSchedule schedule) => new(
        user.Id,
        user.Username,
        schedule.DestinationPath,
        schedule.RetentionCount,
        schedule.FileNamePrefix,
        schedule.FileNameSuffix,
        schedule.LastRunAt,
        schedule.LastRunStatus,
        schedule.LastRunMessage);

    private static string BuildFileName(User user, BackupSchedule schedule, DateTime now)
    {
        var date = now.ToString("yyyyMMdd-HHmmss");
        var username = SanitizeFilePart(user.Username);
        var prefix = string.IsNullOrWhiteSpace(schedule.FileNamePrefix) ? "" : $"{schedule.FileNamePrefix}-";
        var suffix = string.IsNullOrWhiteSpace(schedule.FileNameSuffix) ? "" : $"-{schedule.FileNameSuffix}";
        return $"{prefix}{username}-{date}{suffix}.json";
    }

    private static string SanitizeFilePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        return string.Concat(value.Trim().Select(character => invalid.Contains(character) ? '-' : character));
    }

    private static void ApplyRetention(string destination, int retentionCount)
    {
        if (retentionCount <= 0)
        {
            return;
        }

        foreach (var oldFile in Directory.GetFiles(destination)
            .Where(file => Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file).Equals(".sqlite", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetCreationTimeUtc)
            .Skip(retentionCount))
        {
            File.Delete(oldFile);
        }
    }
}
