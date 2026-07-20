using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface IZoneService
{
    Task<IReadOnlyList<ZoneResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<ZoneResponse> CreateAsync(Guid userId, CreateZoneRequest request, CancellationToken cancellationToken);
    Task<ZoneResponse> UpdateAsync(Guid userId, Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken);
    Task ArchiveAsync(Guid userId, Guid zoneId, CancellationToken cancellationToken);
}
