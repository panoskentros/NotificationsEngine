namespace NotificationEngine.Application.Events;

public record NotificationRequestedEvent(Guid UserId, string Title, string Message);
