using Microsoft.EntityFrameworkCore;

namespace Energieonderbrekingen.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subscription>(e =>
        {
            e.Property(s => s.Email).HasMaxLength(254).IsRequired();
            e.Property(s => s.PostalCode).HasMaxLength(6).IsRequired();
            e.Property(s => s.UnsubscribeToken).HasMaxLength(64).IsRequired();
            e.HasIndex(s => new { s.Email, s.PostalCode }).IsUnique();
            e.HasIndex(s => s.UnsubscribeToken).IsUnique();
            e.HasMany(s => s.Notifications).WithOne().HasForeignKey(n => n.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationLog>(e =>
        {
            e.Property(n => n.DisruptionId).HasMaxLength(64).IsRequired();
            e.Property(n => n.Kind).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(n => new { n.SubscriptionId, n.DisruptionId, n.Kind }).IsUnique();
        });
    }
}

public sealed class Subscription
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string PostalCode { get; set; }
    public required string UnsubscribeToken { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<NotificationLog> Notifications { get; set; } = [];
}

public sealed class NotificationLog
{
    public int Id { get; set; }
    public int SubscriptionId { get; set; }
    public required string DisruptionId { get; set; }
    public NotificationKind Kind { get; set; }
    public DateTimeOffset SentAt { get; set; }
}

public enum NotificationKind
{
    Started,
    Resolved,
}
