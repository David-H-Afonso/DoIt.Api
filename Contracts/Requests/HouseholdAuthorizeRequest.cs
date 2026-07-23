namespace DoIt.Api.Contracts.Requests;

public sealed record HouseholdAuthorizeRequest(
    string ClientId,
    string RedirectUri,
    string State,
    string CodeChallenge,
    string CodeChallengeMethod,
    IReadOnlyCollection<string>? Scopes,
    bool? Approved = null);
