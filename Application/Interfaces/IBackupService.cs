using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface IBackupService
{
    Task<IReadOnlyList<BackupScheduleResponse>> ListAsync(CancellationToken cancellationToken);
    Task<BackupScheduleResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<BackupScheduleResponse> UpdateAsync(Guid userId, UpdateBackupScheduleRequest request, CancellationToken cancellationToken);
    Task<BackupScheduleResponse> RunNowAsync(Guid userId, CancellationToken cancellationToken);
    Task<FullBackupResponse> RunFullNowAsync(Guid userId, CancellationToken cancellationToken);
}
