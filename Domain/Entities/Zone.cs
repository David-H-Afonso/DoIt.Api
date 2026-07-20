namespace DoIt.Api.Domain.Entities;

public sealed class Zone
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? CreatedByUser { get; set; }
    public ICollection<DoItTask> Tasks { get; set; } = new List<DoItTask>();
}
