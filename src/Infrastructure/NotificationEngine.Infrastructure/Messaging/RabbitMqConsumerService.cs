using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using NotificationEngine.Application.Events;
using NotificationEngine.Application.Interfaces;
using NotificationEngine.Domain.Entities;

namespace NotificationEngine.Infrastructure.Messaging;

public class RabbitMqConsumerService : BackgroundService
{
    private const string QueueName = "notifications_queue";
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqConsumerService(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:HostName"] ?? "localhost"
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine("[RabbitMQ Consumer] Bulk message received.");

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var bulkEvent = JsonSerializer.Deserialize<BulkNotificationRequestedEvent>(message, options);

                if (bulkEvent != null)
                {
                    var stopwatch = Stopwatch.StartNew();

                    using var scope = _serviceProvider.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
                    var notifier = scope.ServiceProvider.GetRequiredService<INotificationNotifier>();

                    int chunkSize = 250_000;
                    for (int i = 0; i < bulkEvent.Count; i += chunkSize)
                    {
                        var currentChunkSize = Math.Min(chunkSize, bulkEvent.Count - i);
                        var chunkStream = StreamNotifications(bulkEvent, i, currentChunkSize);

                        await repository.BulkInsertOptimizedAsync(chunkStream, stoppingToken);
                        Console.WriteLine($"[RabbitMQ Consumer] Committed chunk of {currentChunkSize} records.");
                    }
                    await repository.UpdateStatisticsAsync(stoppingToken);
                    await notifier.NotifyAllAsync(stoppingToken);

                    stopwatch.Stop();

                    Console.WriteLine($"[RabbitMQ Consumer] Successfully batch-saved {bulkEvent.Count} notifications to database.");
                    Console.WriteLine($"[RabbitMQ Consumer] SignalR Broadcast sent.");
                    Console.WriteLine($"[RabbitMQ Consumer] Total processing time: {stopwatch.ElapsedMilliseconds} ms ({stopwatch.Elapsed.TotalSeconds:F2} seconds)");
                }

                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[RabbitMQ Consumer] ERROR processing batch: " + ex.Message);
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        Console.WriteLine("[RabbitMQ Consumer] Started listening to queue...");
    }

    private static IEnumerable<Notification> StreamNotifications(BulkNotificationRequestedEvent bulkEvent, int startIndex, int count)
    {
        for (int i = startIndex; i < startIndex + count; i++)
        {
            var title = bulkEvent.Count > 1 ? bulkEvent.Title + " #" + (i + 1) : bulkEvent.Title;
            yield return new Notification(bulkEvent.UserId, title, bulkEvent.Message);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
