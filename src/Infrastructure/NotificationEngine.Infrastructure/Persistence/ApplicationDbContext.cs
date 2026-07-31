using Microsoft.EntityFrameworkCore;
using NotificationEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NotificationEngine.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(utcConverter);
                }
            }
        }

        modelBuilder.Entity<Notification>().HasKey(n => n.Id);


        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.CreatedAtUtc)
            .IncludeProperties(n => new { n.Id, n.UserId, n.Title, n.Message, n.IsRead });
    }
}
