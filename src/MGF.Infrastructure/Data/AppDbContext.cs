namespace MGF.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using MGF.Domain.Entities;
using MGF.Infrastructure.Data.SchemaDocs;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Person> People => Set<Person>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        SchemaDocModelBuilder.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}

