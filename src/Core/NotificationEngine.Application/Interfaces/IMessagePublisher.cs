using NotificationEngine.Application.Events;

namespace NotificationEngine.Application.Interfaces;

public interface IMessagePublisher
{
    Task PublishNotificationAsync(NotificationRequestedEvent notificationEvent, CancellationToken cancellationToken = default);
    Task PublishBulkNotificationAsync(BulkNotificationRequestedEvent bulkEvent, CancellationToken cancellationToken = default);
}
