using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class XpService(DoItDbContext dbContext) : IXpService
{
    public async Task<(int Amount, UserXpResponse UserXp)> AwardCompletionAsync(TaskOccurrence occurrence, TaskCompletion completion, CancellationToken cancellationToken)
    {
        if (completion.Action != TaskCompletionAction.Done || occurrence.Task is null)
        {
            var userXp = await GetUserXpAsync(completion.UserId, cancellationToken);
            return (0, userXp);
        }

        var existing = await dbContext.XpEvents.FirstOrDefaultAsync(xpEvent => xpEvent.CompletionId == completion.Id, cancellationToken);
        if (existing is not null)
        {
            var userXp = await GetUserXpAsync(completion.UserId, cancellationToken);
            return (existing.RevertedAt is null ? existing.Amount : 0, userXp);
        }

        var amount = CalculateAmount(occurrence.Task.Complexity, occurrence.Task.Importance);
        var now = DateTime.UtcNow;
        dbContext.XpEvents.Add(new XpEvent
        {
            Id = Guid.NewGuid(),
            UserId = completion.UserId,
            OccurrenceId = occurrence.Id,
            TaskId = occurrence.TaskId,
            CompletionId = completion.Id,
            Amount = amount,
            Reason = "task_done",
            Complexity = occurrence.Task.Complexity.ToString(),
            Importance = occurrence.Task.Importance.ToString(),
            FormulaVersion = 1,
            CreatedAt = now
        });

        var xp = await GetOrCreateUserXpEntityAsync(completion.UserId, now, cancellationToken);
        xp.TotalXp += amount;
        xp.WeeklyXp += amount;
        xp.CurrentLevel = CalculateLevel(xp.TotalXp);
        xp.UpdatedAt = now;

        return (amount, ToResponse(xp));
    }

    public async Task<UserXpResponse?> RevertCompletionAsync(TaskCompletion completion, CancellationToken cancellationToken)
    {
        var xpEvent = await dbContext.XpEvents.FirstOrDefaultAsync(candidate => candidate.CompletionId == completion.Id && candidate.RevertedAt == null, cancellationToken);
        if (xpEvent is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        xpEvent.RevertedAt = now;
        var xp = await GetOrCreateUserXpEntityAsync(completion.UserId, now, cancellationToken);
        xp.TotalXp = Math.Max(0, xp.TotalXp - xpEvent.Amount);
        xp.WeeklyXp = Math.Max(0, xp.WeeklyXp - xpEvent.Amount);
        xp.CurrentLevel = CalculateLevel(xp.TotalXp);
        xp.UpdatedAt = now;
        return ToResponse(xp);
    }

    public async Task<UserXpResponse> GetUserXpAsync(Guid userId, CancellationToken cancellationToken)
    {
        var xp = await GetOrCreateUserXpEntityAsync(userId, DateTime.UtcNow, cancellationToken);
        return ToResponse(xp);
    }

    private async Task<UserXp> GetOrCreateUserXpEntityAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        var xp = await dbContext.UserXp.FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
        if (xp is not null)
        {
            return xp;
        }

        xp = new UserXp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentLevel = 1,
            UpdatedAt = now
        };
        dbContext.UserXp.Add(xp);
        return xp;
    }

    private static int CalculateAmount(TaskComplexity complexity, TaskImportance importance)
    {
        var baseXp = complexity switch
        {
            TaskComplexity.VeryQuick => 5,
            TaskComplexity.Easy => 10,
            TaskComplexity.Medium => 20,
            TaskComplexity.Heavy => 35,
            TaskComplexity.SmallProject => 50,
            _ => 10
        };
        var multiplier = importance switch
        {
            TaskImportance.Low => 0.75m,
            TaskImportance.Normal => 1m,
            TaskImportance.High => 1.25m,
            TaskImportance.Critical => 1.5m,
            _ => 1m
        };
        return (int)Math.Round(baseXp * multiplier, MidpointRounding.AwayFromZero);
    }

    private static int CalculateLevel(int totalXp) => Math.Max(1, (int)Math.Floor(Math.Sqrt(totalXp / 100d)) + 1);

    private static UserXpResponse ToResponse(UserXp xp)
    {
        var currentLevelXp = (int)Math.Pow(xp.CurrentLevel - 1, 2) * 100;
        var nextLevelXp = (int)Math.Pow(xp.CurrentLevel, 2) * 100;
        return new UserXpResponse(
            xp.TotalXp,
            xp.WeeklyXp,
            xp.CurrentLevel,
            currentLevelXp,
            nextLevelXp,
            Math.Clamp(xp.TotalXp - currentLevelXp, 0, nextLevelXp - currentLevelXp));
    }
}
