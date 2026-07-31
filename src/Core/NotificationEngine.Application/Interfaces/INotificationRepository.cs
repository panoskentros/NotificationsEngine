using NotificationEngine.Domain.Entities;

namespace NotificationEngine.Application.Interfaces;

public interface INotificationRepository
{
    #region Standard Operations
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);
    Task<IEnumerable<Notification>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Notification>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Notification> Items, int TotalCount)> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);
    void Update(Notification notification);
    void Delete(Notification notification);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    #endregion

    #region High-Performance/Bulk Operations
    Task UpdateStatisticsAsync(CancellationToken cancellationToken = default);
    Task DeleteAllAsync(CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(CancellationToken cancellationToken = default);
    Task BulkInsertOptimizedAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);
    #endregion
}
