namespace DoIt.Api.Contracts.Requests;

public sealed record HouseholdRevokeRequest(string Token, string? TokenTypeHint = null);
