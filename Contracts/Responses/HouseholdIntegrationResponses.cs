namespace DoIt.Api.Contracts.Responses;

public sealed record HouseholdAuthorizeResponse(string RedirectUrl);

public sealed record HouseholdIntegrationAccountResponse(string Id, string DisplayName);

public sealed record HouseholdTokenResponse(
    string TokenType,
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    int RefreshExpiresIn,
    string Scope,
    Guid ConnectionId,
    HouseholdIntegrationAccountResponse Account);

public sealed record HouseholdMeResponse(
    Guid ConnectionId,
    HouseholdIntegrationAccountResponse Account,
    IReadOnlyCollection<string> Scopes);
