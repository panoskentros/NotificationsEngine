using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NotificationEngine.Api.Hubs;
using NotificationEngine.Application.Events;
using NotificationEngine.Application.Interfaces;
using NotificationEngine.Domain.Entities;

namespace NotificationEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IMessagePublisher _messagePublisher;
    private readonly INotificationRepository _repository;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationsController(
        IMessagePublisher messagePublisher,
        INotificationRepository repository,
        IHubContext<NotificationHub> hubContext)
    {
        _messagePublisher = messagePublisher;
        _repository = repository;
        _hubContext = hubContext;
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> QueueBulkNotifications([FromBody] BulkNotificationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"[API] Queueing bulk request for {request.Count} notifications...");

            var bulkEvent = new BulkNotificationRequestedEvent(
                request.UserId,
                request.Title,
                request.Message,
                request.Count
            );

            await _messagePublisher.PublishBulkNotificationAsync(bulkEvent, cancellationToken);

            Console.WriteLine($"[API] Bulk request successfully queued.");
            return Accepted(new { Message = $"{request.Count} notifications queued successfully." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API CRITICAL ERROR in Bulk]: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _repository.GetPagedAsync(skip, take, cancellationToken);
        return Ok(new { Items = items, TotalCount = total });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var notification = await _repository.GetByIdAsync(id, cancellationToken);
        if (notification == null) return NotFound();

        notification.IsRead = true;
        _repository.Update(notification);
        await _repository.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.All.SendAsync("ReceiveNotificationUpdate", cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var notification = await _repository.GetByIdAsync(id, cancellationToken);
        if (notification == null) return NotFound();

        _repository.Delete(notification);
        await _repository.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.All.SendAsync("ReceiveNotificationUpdate", cancellationToken);
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await _repository.MarkAllAsReadAsync(cancellationToken);

        await _hubContext.Clients.All.SendAsync("ReceiveNotificationUpdate", cancellationToken);
        return NoContent();
    }

    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken)
    {
        await _repository.DeleteAllAsync(cancellationToken);

        await _hubContext.Clients.All.SendAsync("ReceiveNotificationUpdate", cancellationToken);
        return NoContent();
    }
}

public record BulkNotificationRequest(Guid UserId, string Title, string Message, int Count);
