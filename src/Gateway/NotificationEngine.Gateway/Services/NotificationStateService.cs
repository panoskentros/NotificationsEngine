using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace NotificationEngine.Gateway.Services;

public class NotificationStateService : IAsyncDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private HubConnection? _hubConnection;
    private bool _isInitialized;

    public int TotalCount { get; private set; }
    public event Action? OnChange;

    public NotificationStateService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("http://127.0.0.1:5110/notificationhub")
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On("ReceiveNotificationUpdate", () =>
            {
                Console.WriteLine("=> [Gateway] SignalR Update Received! Forcing UI to refresh...");
                NotifyStateChanged();
            });

            await _hubConnection.StartAsync();
            _isInitialized = true;
            Console.WriteLine("SignalR Connected Successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignalR Connection Error: {ex.Message}");
        }
    }

    private async Task<HttpClient> GetClientAsync()
    {
        await InitializeAsync();
        return _httpClientFactory.CreateClient("ApiClient");
    }

    public async Task<ItemsProviderResult<NotificationDto>> GetNotificationsProviderAsync(ItemsProviderRequest request)
    {
        try
        {
            var client = await GetClientAsync();
            var response = await client.GetFromJsonAsync<PagedNotificationDto>($"api/notifications?skip={request.StartIndex}&take={request.Count}");

            if (response != null)
            {
                TotalCount = response.TotalCount;
                return new ItemsProviderResult<NotificationDto>(response.Items, response.TotalCount);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading notifications page: {ex.Message}");
        }

        return new ItemsProviderResult<NotificationDto>(new List<NotificationDto>(), 0);
    }

    public async Task CreateBulkNotificationsAsync(string title, string message, int count)
    {
        var client = await GetClientAsync();
        var request = new BulkNotificationRequest(Guid.NewGuid(), title, message, count);
        await client.PostAsJsonAsync("api/notifications/bulk", request);
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var client = await GetClientAsync();
        var response = await client.PutAsync($"api/notifications/{id}/read", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteNotificationAsync(Guid id)
    {
        var client = await GetClientAsync();
        var response = await client.DeleteAsync($"api/notifications/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkAllAsReadAsync()
    {
        var client = await GetClientAsync();
        var response = await client.PutAsync("api/notifications/read-all", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAllAsync()
    {
        var client = await GetClientAsync();
        var response = await client.DeleteAsync("api/notifications/all");
        response.EnsureSuccessStatusCode();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}

public class PagedNotificationDto
{
    public List<NotificationDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public record BulkNotificationRequest(Guid UserId, string Title, string Message, int Count);

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
