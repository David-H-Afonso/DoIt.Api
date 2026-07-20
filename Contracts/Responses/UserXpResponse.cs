namespace DoIt.Api.Contracts.Responses;

public sealed record UserXpResponse(
    int TotalXp,
    int WeeklyXp,
    int CurrentLevel,
    int CurrentLevelXp,
    int NextLevelXp,
    int ProgressToNextLevel);
