namespace MGF.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using MGF.Domain.Entities;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<SquareWebhookEvent> SquareWebhookEvents => Set<SquareWebhookEvent>();
    public DbSet<SquareSyncReviewQueueItem> SquareSyncReviewQueue => Set<SquareSyncReviewQueueItem>();
    public DbSet<SquareReconcileCursor> SquareReconcileCursors => Set<SquareReconcileCursor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        AppDbContextModelBuilder.Apply(modelBuilder);
    }
}
