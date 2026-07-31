using Microsoft.EntityFrameworkCore;
using NotificationEngine.Application.Interfaces;
using NotificationEngine.Domain.Entities;
using Npgsql;

namespace NotificationEngine.Infrastructure.Persistence;

public class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _dbContext;
    public NotificationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    #region Standard Operations
    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications.AddAsync(notification, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications.AddRangeAsync(notifications, cancellationToken);
    }

    public async Task<IEnumerable<Notification>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications.ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Notification>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
    #endregion
    public async Task BulkInsertOptimizedAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
    {
        var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using (var writer = await connection.BeginBinaryImportAsync(
                         "COPY \"Notifications\" (\"Id\", \"UserId\", \"Title\", \"Message\", \"IsRead\", \"CreatedAtUtc\") FROM STDIN (FORMAT BINARY)",
                         cancellationToken))
        {
            foreach (var n in notifications)
            {
                writer.StartRow();
                writer.Write(n.Id, NpgsqlTypes.NpgsqlDbType.Uuid);
                writer.Write(n.UserId, NpgsqlTypes.NpgsqlDbType.Uuid);
                writer.Write(n.Title, NpgsqlTypes.NpgsqlDbType.Text);
                writer.Write(n.Message, NpgsqlTypes.NpgsqlDbType.Text);
                writer.Write(n.IsRead, NpgsqlTypes.NpgsqlDbType.Boolean);

                var utcDate = n.CreatedAtUtc.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(n.CreatedAtUtc, DateTimeKind.Utc)
                    : n.CreatedAtUtc.ToUniversalTime();

                writer.Write(utcDate, NpgsqlTypes.NpgsqlDbType.TimestampTz);
            }

            await writer.CompleteAsync(cancellationToken);
        }
    }
    public async Task UpdateStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "ANALYZE \"Notifications\";";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task<(IEnumerable<Notification> Items, int TotalCount)> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var totalCount = await _dbContext.Database
            .SqlQueryRaw<int>("SELECT CAST(reltuples AS integer) AS \"Value\" FROM pg_class WHERE relname ILIKE 'notifications'")
            .FirstOrDefaultAsync(cancellationToken);

        var items = await _dbContext.Notifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications.FindAsync(new object[] { id });
    }
    public void Update(Notification notification)
    {
        _dbContext.Notifications.Update(notification);
    }
    public void Delete(Notification notification)
    {
        _dbContext.Notifications.Remove(notification);
    }
    public async Task MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);
    }
    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync("ANALYZE \"Notifications\";"); // to refresh Postgre cache of reltuples
    }
}
