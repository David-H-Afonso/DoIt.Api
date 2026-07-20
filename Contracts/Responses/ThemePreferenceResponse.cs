namespace DoIt.Api.Contracts.Responses;

public sealed record ThemePreferenceResponse(
    string ThemeMode,
    string PrimaryColor,
    string AccentColor,
    string BackgroundColor,
    string SurfaceColor,
    string? TextColor,
    string? BackgroundImagePath,
    string BackgroundOverlayColor,
    decimal BackgroundOverlayOpacity);
