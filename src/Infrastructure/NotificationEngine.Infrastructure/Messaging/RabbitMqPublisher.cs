using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using NotificationEngine.Application.Events;
using NotificationEngine.Application.Interfaces;

namespace NotificationEngine.Infrastructure.Messaging;

public class RabbitMqPublisher : IMessagePublisher, IAsyncDisposable
{
    private const string QueueName = "notifications_queue";
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqPublisher(IConfiguration configuration)
    {
        _factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost"
        };
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            _connection = await _factory.CreateConnectionAsync(cancellationToken);
        }

        if (_channel is null)
        {
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await _channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        }

        return _channel;
    }

    public Task PublishNotificationAsync(NotificationRequestedEvent notificationEvent, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[RabbitMQ Publisher] Preparing to send: {notificationEvent.Title}");
        return PublishInternalAsync(notificationEvent, cancellationToken);
    }

    public Task PublishBulkNotificationAsync(BulkNotificationRequestedEvent bulkEvent, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[RabbitMQ Publisher] Preparing to send bulk request for {bulkEvent.Count} items: {bulkEvent.Title}");
        return PublishInternalAsync(bulkEvent, cancellationToken);
    }

    private async Task PublishInternalAsync<T>(T messageEvent, CancellationToken cancellationToken)
    {
        try
        {
            var channel = await GetChannelAsync(cancellationToken);

            var message = JsonSerializer.Serialize(messageEvent);
            var body = Encoding.UTF8.GetBytes(message);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: QueueName,
                body: body,
                cancellationToken: cancellationToken);

            Console.WriteLine($"[RabbitMQ Publisher] Successfully published to '{QueueName}'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RabbitMQ Publisher] ERROR: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
    }
}
