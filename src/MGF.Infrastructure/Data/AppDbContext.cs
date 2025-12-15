namespace MGF.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

    public DbSet<ProjectStatus> ProjectStatuses => Set<ProjectStatus>();
    public DbSet<ProjectPhase> ProjectPhases => Set<ProjectPhase>();
    public DbSet<ProjectPriority> ProjectPriorities => Set<ProjectPriority>();
    public DbSet<ProjectType> ProjectTypes => Set<ProjectType>();

    public DbSet<ProjectRole> ProjectRoles => Set<ProjectRole>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<ProjectCodeCounter> ProjectCodeCounters => Set<ProjectCodeCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("clients");

            entity.HasKey(x => x.CliId);
            entity.Property(x => x.CliId).HasColumnName("cli_id").IsRequired().ValueGeneratedNever();
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();

            ConfigureAuditColumns(entity);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");

            entity.HasKey(x => x.PrjId);
            entity.Property(x => x.PrjId).HasColumnName("prj_id").IsRequired().ValueGeneratedNever();

            entity.Property(x => x.ProjectCode).HasColumnName("project_code").IsRequired();
            entity.HasIndex(x => x.ProjectCode).IsUnique();

            entity.Property(x => x.CliId).HasColumnName("cli_id").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();

            entity.Property(x => x.StatusKey).HasColumnName("status_key").IsRequired();
            entity.Property(x => x.PhaseKey).HasColumnName("phase_key").IsRequired();
            entity.Property(x => x.PriorityKey).HasColumnName("priority_key");
            entity.Property(x => x.TypeKey).HasColumnName("type_key");

            entity.Property(x => x.PathsRootKey).HasColumnName("paths_root_key").IsRequired();
            entity.Property(x => x.FolderRelpath).HasColumnName("folder_relpath").IsRequired();
            entity.Property(x => x.DropboxUrl).HasColumnName("dropbox_url");
            entity.Property(x => x.ArchivedAt).HasColumnName("archived_at").HasColumnType("timestamptz");

            ConfigureAuditColumns(entity);

            entity.HasOne<Client>()
                .WithMany()
                .HasForeignKey(x => x.CliId)
                .HasPrincipalKey(x => x.CliId)
                .IsRequired();

            entity.HasOne<ProjectStatus>()
                .WithMany()
                .HasForeignKey(x => x.StatusKey)
                .HasPrincipalKey(x => x.StatusKey)
                .IsRequired();

            entity.HasOne<ProjectPhase>()
                .WithMany()
                .HasForeignKey(x => x.PhaseKey)
                .HasPrincipalKey(x => x.PhaseKey)
                .IsRequired();

            entity.HasOne<ProjectPriority>()
                .WithMany()
                .HasForeignKey(x => x.PriorityKey)
                .HasPrincipalKey(x => x.PriorityKey);

            entity.HasOne<ProjectType>()
                .WithMany()
                .HasForeignKey(x => x.TypeKey)
                .HasPrincipalKey(x => x.TypeKey);
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("people");

            entity.HasKey(x => x.PerId);
            entity.Property(x => x.PerId).HasColumnName("per_id").IsRequired().ValueGeneratedNever();

            entity.Property(x => x.Initials).HasColumnName("initials").IsRequired();

            ConfigureAuditColumns(entity);
        });

        modelBuilder.Entity<ProjectStatus>(entity =>
        {
            entity.ToTable("project_statuses");

            entity.HasKey(x => x.StatusKey);
            entity.Property(x => x.StatusKey).HasColumnName("status_key").IsRequired().ValueGeneratedNever();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0).IsRequired();
        });

        modelBuilder.Entity<ProjectPhase>(entity =>
        {
            entity.ToTable("project_phases");

            entity.HasKey(x => x.PhaseKey);
            entity.Property(x => x.PhaseKey).HasColumnName("phase_key").IsRequired().ValueGeneratedNever();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0).IsRequired();
        });

        modelBuilder.Entity<ProjectPriority>(entity =>
        {
            entity.ToTable("project_priorities");

            entity.HasKey(x => x.PriorityKey);
            entity.Property(x => x.PriorityKey).HasColumnName("priority_key").IsRequired().ValueGeneratedNever();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0).IsRequired();
        });

        modelBuilder.Entity<ProjectType>(entity =>
        {
            entity.ToTable("project_types");

            entity.HasKey(x => x.TypeKey);
            entity.Property(x => x.TypeKey).HasColumnName("type_key").IsRequired().ValueGeneratedNever();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0).IsRequired();
        });

        modelBuilder.Entity<ProjectRole>(entity =>
        {
            entity.ToTable("project_roles");

            entity.HasKey(x => x.RoleKey);
            entity.Property(x => x.RoleKey).HasColumnName("role_key").IsRequired().ValueGeneratedNever();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0).IsRequired();
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.ToTable("project_members");

            entity.HasKey(x => new { x.PrjId, x.PerId, x.RoleKey, x.AssignedAt });

            entity.Property(x => x.PrjId).HasColumnName("prj_id").IsRequired();
            entity.Property(x => x.PerId).HasColumnName("per_id").IsRequired();
            entity.Property(x => x.RoleKey).HasColumnName("role_key").IsRequired();

            entity.Property(x => x.AssignedAt)
                .HasColumnName("assigned_at")
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("now()")
                .IsRequired();

            entity.Property(x => x.ReleasedAt)
                .HasColumnName("released_at")
                .HasColumnType("timestamptz");

            entity.HasIndex(x => new { x.PrjId, x.PerId, x.RoleKey })
                .IsUnique()
                .HasFilter("released_at IS NULL");

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.PrjId)
                .HasPrincipalKey(x => x.PrjId)
                .IsRequired();

            entity.HasOne<Person>()
                .WithMany()
                .HasForeignKey(x => x.PerId)
                .HasPrincipalKey(x => x.PerId)
                .IsRequired();

            entity.HasOne<ProjectRole>()
                .WithMany()
                .HasForeignKey(x => x.RoleKey)
                .HasPrincipalKey(x => x.RoleKey)
                .IsRequired();
        });

        modelBuilder.Entity<ProjectCodeCounter>(entity =>
        {
            entity.ToTable("project_code_counters");

            entity.HasKey(x => x.Year);
            entity.Property(x => x.Year).HasColumnName("year").IsRequired().ValueGeneratedNever();
            entity.Property(x => x.NextSeq).HasColumnName("next_seq").IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureAuditColumns<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : EntityBase
    {
        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .IsRequired();

        entity.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .IsRequired();
    }
}
