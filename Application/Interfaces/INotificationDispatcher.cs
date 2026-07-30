namespace DoIt.Api.Application.Interfaces;

public interface INotificationDispatcher
{
    Task<int> DispatchAsync(CancellationToken cancellationToken);
}
