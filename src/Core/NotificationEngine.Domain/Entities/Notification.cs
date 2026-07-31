namespace NotificationEngine.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    protected Notification() { }

    public Notification(Guid userId, string title, string message)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Title = title;
        Message = message;
        IsRead = false;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
