namespace MGF.Data.Stores.Projects;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MGF.Contracts.Abstractions.Projects;
using MGF.Data.Data;
using MGF.Data.Stores.Counters;
using MGF.Domain.Entities;

public sealed class ProjectCreationStore : IProjectCreationStore
{
    private readonly AppDbContext db;
    private readonly ICounterAllocator counterAllocator;

    public ProjectCreationStore(AppDbContext db, ICounterAllocator counterAllocator)
    {
        this.db = db;
        this.counterAllocator = counterAllocator;
    }

    public Task<bool> ClientExistsAsync(string clientId, CancellationToken cancellationToken = default)
    {
        return db.Clients.AsNoTracking().AnyAsync(c => c.ClientId == clientId, cancellationToken);
    }

    public Task<bool> PersonExistsAsync(string personId, CancellationToken cancellationToken = default)
    {
        return db.People.AsNoTracking().AnyAsync(p => p.PersonId == personId, cancellationToken);
    }

    public Task<bool> PersonHasRoleAsync(
        string personId,
        string roleKey,
        CancellationToken cancellationToken = default)
    {
        return db
            .Set<Dictionary<string, object>>("person_roles")
            .AsNoTracking()
            .AnyAsync(
                r => EF.Property<string>(r, "person_id") == personId && EF.Property<string>(r, "role_key") == roleKey,
                cancellationToken
            );
    }

    public async Task<IReadOnlyList<string>> GetPersonRolesAsync(
        string personId,
        CancellationToken cancellationToken = default)
    {
        return await db
            .Set<Dictionary<string, object>>("person_roles")
            .AsNoTracking()
            .Where(r => EF.Property<string>(r, "person_id") == personId)
            .Select(r => EF.Property<string>(r, "role_key"))
            .ToListAsync(cancellationToken);
    }

    public async Task CreateProjectAsync(
        CreateProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var projectCode = await counterAllocator.AllocateProjectCodeAsync(cancellationToken);
            var metadata = JsonDocument.Parse("{}").RootElement.Clone();

            db.Add(
                new Project(
                    projectId: command.ProjectId,
                    projectCode: projectCode,
                    clientId: command.ClientId,
                    name: command.ProjectName,
                    statusKey: "active",
                    phaseKey: "planning",
                    priorityKey: "normal",
                    dataProfile: "real",
                    metadata: metadata
                )
            );

            await db.SaveChangesAsync(cancellationToken);

            await AddProjectMemberAsync(command.ProjectId, command.EditorPersonId, "editor", cancellationToken);

            foreach (var roleKey in command.AdditionalRoleKeys.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await AddProjectMemberAsync(command.ProjectId, command.EditorPersonId, roleKey, cancellationToken);
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                ProjectCreationSql.BuildEnqueueProjectCreationJobCommand(
                    command.JobId,
                    command.PayloadJson,
                    command.ProjectId
                ),
                cancellationToken
            );

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private Task AddProjectMemberAsync(
        string projectId,
        string personId,
        string roleKey,
        CancellationToken cancellationToken)
    {
        var projectMemberId = EntityIds.NewWithPrefix("prm");

        return db.Database.ExecuteSqlInterpolatedAsync(
            ProjectCreationSql.BuildInsertProjectMemberCommand(
                projectMemberId,
                projectId,
                personId,
                roleKey
            ),
            cancellationToken
        );
    }
}

internal static class ProjectCreationSql
{
    internal static FormattableString BuildInsertProjectMemberCommand(
        string projectMemberId,
        string projectId,
        string personId,
        string roleKey)
    {
        return $"""
        INSERT INTO public.project_members (project_member_id, project_id, person_id, role_key, assigned_at)
        VALUES ({projectMemberId}, {projectId}, {personId}, {roleKey}, now());
        """;
    }

    internal static FormattableString BuildEnqueueProjectCreationJobCommand(
        string jobId,
        string payloadJson,
        string projectId)
    {
        return $"""
        INSERT INTO public.jobs (job_id, job_type_key, payload, status_key, run_after, entity_type_key, entity_key)
        VALUES ({jobId}, {"dropbox.create_project_structure"}, {payloadJson}::jsonb, {"queued"}, now(), {"project"}, {projectId});
        """;
    }
}
