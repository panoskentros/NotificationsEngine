namespace NotificationEngine.Application.Events;

public record BulkNotificationRequestedEvent(Guid UserId, string Title, string Message, int Count);
