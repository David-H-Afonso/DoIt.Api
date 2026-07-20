using DoIt.Api.Application.Interfaces;
using DoIt.Api.Application.Mapping;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class ZoneService(DoItDbContext dbContext) : IZoneService
{
    public async Task<IReadOnlyList<ZoneResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var zones = await dbContext.Zones
            .Where(zone => zone.CreatedByUserId == userId)
            .OrderBy(zone => zone.IsArchived)
            .ThenBy(zone => zone.SortOrder)
            .ThenBy(zone => zone.Name)
            .ToListAsync(cancellationToken);

        return zones.Select(zone => zone.ToResponse()).ToList();
    }

    public async Task<ZoneResponse> CreateAsync(Guid userId, CreateZoneRequest request, CancellationToken cancellationToken)
    {
        ValidateName(request.Name);
        var now = DateTime.UtcNow;
        var sortOrder = request.SortOrder ?? await GetNextSortOrderAsync(userId, cancellationToken);

        var zone = new Zone
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            Color = NormalizeOptional(request.Color),
            Icon = NormalizeOptional(request.Icon),
            SortOrder = sortOrder,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Zones.Add(zone);
        await dbContext.SaveChangesAsync(cancellationToken);
        return zone.ToResponse();
    }

    public async Task<ZoneResponse> UpdateAsync(Guid userId, Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken)
    {
        ValidateName(request.Name);
        var zone = await dbContext.Zones.FirstOrDefaultAsync(candidate => candidate.Id == zoneId && candidate.CreatedByUserId == userId, cancellationToken);
        if (zone is null)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "zone_not_found", "Zone not found.");
        }

        zone.Name = request.Name.Trim();
        zone.Description = NormalizeOptional(request.Description);
        zone.Color = NormalizeOptional(request.Color);
        zone.Icon = NormalizeOptional(request.Icon);
        zone.SortOrder = request.SortOrder;
        zone.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return zone.ToResponse();
    }

    public async Task ArchiveAsync(Guid userId, Guid zoneId, CancellationToken cancellationToken)
    {
        var zone = await dbContext.Zones.FirstOrDefaultAsync(candidate => candidate.Id == zoneId && candidate.CreatedByUserId == userId, cancellationToken);
        if (zone is null)
        {
            return;
        }

        zone.IsArchived = true;
        zone.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> GetNextSortOrderAsync(Guid userId, CancellationToken cancellationToken)
    {
        var maxSortOrder = await dbContext.Zones
            .Where(zone => zone.CreatedByUserId == userId)
            .Select(zone => (int?)zone.SortOrder)
            .MaxAsync(cancellationToken);

        return (maxSortOrder ?? -1) + 1;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "zone_name_required", "Zone name is required.");
        }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
