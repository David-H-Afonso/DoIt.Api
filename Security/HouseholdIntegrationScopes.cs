namespace DoIt.Api.Security;

public static class HouseholdIntegrationScopes
{
    public const string ProfileRead = "profile.read";
    public const string TasksRead = "tasks.read";
    public const string TasksComplete = "tasks.complete";
    public const string TasksUndo = "tasks.undo";
    public const string TasksCreate = "tasks.create";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        ProfileRead,
        TasksRead,
        TasksComplete,
        TasksUndo,
        TasksCreate
    };
}
