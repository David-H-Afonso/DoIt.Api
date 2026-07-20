using System.Text.RegularExpressions;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed partial class ThemePreferenceService(DoItDbContext dbContext) : IThemePreferenceService
{
    public async Task<ThemePreferenceResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var preference = await GetOrCreateAsync(userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(preference);
    }

    public async Task<ThemePreferenceResponse> UpdateAsync(Guid userId, UpdateThemePreferenceRequest request, CancellationToken cancellationToken)
    {
        var preference = await GetOrCreateAsync(userId, cancellationToken);
        preference.ThemeMode = NormalizeMode(request.ThemeMode, preference.ThemeMode);
        preference.PrimaryColor = NormalizeColor(request.PrimaryColor, preference.PrimaryColor);
        preference.AccentColor = NormalizeColor(request.AccentColor, preference.AccentColor);
        preference.BackgroundColor = NormalizeColor(request.BackgroundColor, preference.BackgroundColor);
        preference.SurfaceColor = NormalizeColor(request.SurfaceColor, preference.SurfaceColor);
        preference.TextColor = string.IsNullOrWhiteSpace(request.TextColor) ? null : NormalizeColor(request.TextColor, preference.TextColor ?? "#171717");
        preference.BackgroundImagePath = string.IsNullOrWhiteSpace(request.BackgroundImagePath) ? null : request.BackgroundImagePath.Trim();
        preference.BackgroundOverlayColor = NormalizeColor(request.BackgroundOverlayColor, preference.BackgroundOverlayColor);
        preference.BackgroundOverlayOpacity = Math.Clamp(request.BackgroundOverlayOpacity ?? preference.BackgroundOverlayOpacity, 0.35m, 0.95m);
        preference.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(preference);
    }

    private async Task<ThemePreference> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var preference = await dbContext.ThemePreferences.FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
        if (preference is not null)
        {
            return preference;
        }

        var now = DateTime.UtcNow;
        preference = new ThemePreference { Id = Guid.NewGuid(), UserId = userId, CreatedAt = now, UpdatedAt = now };
        dbContext.ThemePreferences.Add(preference);
        return preference;
    }

    private static string NormalizeMode(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "light" or "dark" or "system" or "custom" ? normalized : fallback;
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var normalized = value.Trim();
        if (!HexColorRegex().IsMatch(normalized))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_color", "Theme colors must be hex colors like #2563eb.");
        }
        return normalized;
    }

    private static ThemePreferenceResponse ToResponse(ThemePreference preference) => new(
        preference.ThemeMode,
        preference.PrimaryColor,
        preference.AccentColor,
        preference.BackgroundColor,
        preference.SurfaceColor,
        preference.TextColor,
        preference.BackgroundImagePath,
        preference.BackgroundOverlayColor,
        preference.BackgroundOverlayOpacity);

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColorRegex();
}
