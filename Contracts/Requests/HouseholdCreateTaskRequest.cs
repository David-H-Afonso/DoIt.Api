namespace DoIt.Api.Contracts.Requests;

public sealed record HouseholdCreateTaskRequest(
    string Title,
    string? Description,
    Guid? ZoneId,
    string? TaskType,
    string? Importance,
    string? Complexity,
    string? Obligation,
    TaskScheduleRequest? Schedule);
