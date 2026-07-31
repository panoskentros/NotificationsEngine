using Microsoft.AspNetCore.SignalR;
using NotificationEngine.Api.Hubs;
using NotificationEngine.Application.Interfaces;

namespace NotificationEngine.Api.Services;

public class SignalRNotificationNotifier : INotificationNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyAllAsync(CancellationToken cancellationToken)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveNotificationUpdate", cancellationToken);
    }
}
