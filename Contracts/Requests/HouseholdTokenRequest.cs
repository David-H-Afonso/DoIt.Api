namespace DoIt.Api.Contracts.Requests;

public sealed record HouseholdTokenRequest(
    string GrantType,
    string ClientId,
    string? RedirectUri = null,
    string? Code = null,
    string? CodeVerifier = null,
    string? RefreshToken = null);
