namespace DoIt.Api.Security;

public static class HouseholdIntegrationPolicies
{
    public const string AuthenticationScheme = "HouseholdIntegrationBearer";
    public const string ProfileRead = "HouseholdIntegrationProfileRead";
    public const string TasksRead = "HouseholdIntegrationTasksRead";
    public const string TasksComplete = "HouseholdIntegrationTasksComplete";
    public const string TasksUndo = "HouseholdIntegrationTasksUndo";
    public const string TasksCreate = "HouseholdIntegrationTasksCreate";
}
