namespace DoIt.Api.Contracts.Requests;

public sealed record UpdateThemePreferenceRequest(
    string? ThemeMode,
    string? PrimaryColor,
    string? AccentColor,
    string? BackgroundColor,
    string? SurfaceColor,
    string? TextColor,
    string? BackgroundImagePath,
    string? BackgroundOverlayColor,
    decimal? BackgroundOverlayOpacity);
