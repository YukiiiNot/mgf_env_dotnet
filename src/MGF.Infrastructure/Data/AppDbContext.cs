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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("clients");

            entity.HasKey(x => x.ClientId);
            entity.Property(x => x.ClientId).HasColumnName("client_id").IsRequired().ValueGeneratedNever();
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");

            entity.HasKey(x => x.ProjectId);
            entity.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired().ValueGeneratedNever();

            entity.Property(x => x.ClientId).HasColumnName("client_id").IsRequired();

            entity.HasOne<Client>()
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .HasPrincipalKey(x => x.ClientId)
                .IsRequired();
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("people");

            entity.HasKey(x => x.PersonId);
            entity.Property(x => x.PersonId).HasColumnName("person_id").IsRequired().ValueGeneratedNever();

            entity.Property(x => x.Initials).HasColumnName("initials").IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
