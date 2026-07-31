namespace NotificationEngine.Application.Interfaces;

public interface INotificationNotifier
{
    Task NotifyAllAsync(CancellationToken cancellationToken);
}
