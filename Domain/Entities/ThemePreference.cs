namespace DoIt.Api.Domain.Entities;

public sealed class ThemePreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ThemeMode { get; set; } = "light";
    public string PrimaryColor { get; set; } = "#2563eb";
    public string AccentColor { get; set; } = "#16a34a";
    public string BackgroundColor { get; set; } = "#f7f4ef";
    public string SurfaceColor { get; set; } = "#ffffff";
    public string? TextColor { get; set; }
    public string? BackgroundImagePath { get; set; }
    public string BackgroundOverlayColor { get; set; } = "#f7f4ef";
    public decimal BackgroundOverlayOpacity { get; set; } = 0.78m;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
