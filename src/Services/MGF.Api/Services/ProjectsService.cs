namespace MGF.Api.Services;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MGF.Domain.Entities;
using MGF.Data.Data;

public sealed class ProjectsService
{
    private readonly AppDbContext db;
    private readonly ILogger<ProjectsService> logger;

    public ProjectsService(AppDbContext db, ILogger<ProjectsService> logger)
    {
        this.db = db;
        this.logger = logger;
    }

    public sealed record CreateProjectRequest(string ClientId, string ProjectName, string EditorPersonId, string TemplateKey);

    public sealed record CreateProjectResponse(string ProjectId, string JobId);

    public async Task<CreateProjectResponse?> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(request.EditorPersonId))
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(request.TemplateKey))
        {
            return null;
        }

        var clientExists = await db.Clients.AsNoTracking().AnyAsync(c => c.ClientId == request.ClientId, cancellationToken);
        if (!clientExists)
        {
            return null;
        }

        var editorExists = await db.People.AsNoTracking().AnyAsync(p => p.PersonId == request.EditorPersonId, cancellationToken);
        if (!editorExists)
        {
            return null;
        }

        var isEditor = await db
            .Set<Dictionary<string, object>>("person_roles")
            .AsNoTracking()
            .AnyAsync(
                r => EF.Property<string>(r, "person_id") == request.EditorPersonId && EF.Property<string>(r, "role_key") == "editor",
                cancellationToken
            );
        if (!isEditor)
        {
            return null;
        }

        var projectId = EntityIds.NewProjectId();
        var jobId = EntityIds.NewWithPrefix("job");

        var payloadJson = JsonSerializer.Serialize(
            new { projectId, clientId = request.ClientId, templateKey = request.TemplateKey }
        );

        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var projectCode = await AllocateProjectCodeAsync(cancellationToken);

            var metadata = JsonDocument.Parse("{}").RootElement.Clone();

            db.Add(
                new Project(
                    projectId: projectId,
                    projectCode: projectCode,
                    clientId: request.ClientId,
                    name: request.ProjectName,
                    statusKey: "active",
                    phaseKey: "planning",
                    priorityKey: "normal",
                    dataProfile: "real",
                    metadata: metadata
                )
            );

            await db.SaveChangesAsync(cancellationToken);

            await AddProjectMemberAsync(projectId, request.EditorPersonId, roleKey: "editor", cancellationToken);

            var extraRoles = await db
                .Set<Dictionary<string, object>>("person_roles")
                .AsNoTracking()
                .Where(
                    r =>
                        EF.Property<string>(r, "person_id") == request.EditorPersonId
                        && (EF.Property<string>(r, "role_key") == "producer" || EF.Property<string>(r, "role_key") == "admin")
                )
                .Select(r => EF.Property<string>(r, "role_key"))
                .ToListAsync(cancellationToken);

            foreach (var roleKey in extraRoles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await AddProjectMemberAsync(projectId, request.EditorPersonId, roleKey, cancellationToken);
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO public.jobs (job_id, job_type_key, payload, status_key, run_after, entity_type_key, entity_key)
                VALUES ({jobId}, {"dropbox.create_project_structure"}, {payloadJson}::jsonb, {"queued"}, now(), {"project"}, {projectId});
                """,
                cancellationToken
            );

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("MGF.Api: created project {ProjectId} and enqueued job {JobId}", projectId, jobId);

            return new CreateProjectResponse(projectId, jobId);
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

    private async Task<string> AllocateProjectCodeAsync(CancellationToken cancellationToken)
    {
        return await db.Database
            .SqlQueryRaw<string>(
                """
                WITH ensured AS (
                  INSERT INTO public.project_code_counters(prefix, year_2, next_seq)
                  VALUES ('MGF', (EXTRACT(YEAR FROM now())::int % 100)::smallint, 1)
                  ON CONFLICT (prefix, year_2) DO NOTHING
                ),
                updated AS (
                  UPDATE public.project_code_counters
                  SET next_seq = next_seq + 1, updated_at = now()
                  WHERE prefix = 'MGF' AND year_2 = (EXTRACT(YEAR FROM now())::int % 100)::smallint
                  RETURNING year_2, (next_seq - 1) AS allocated_seq
                )
                SELECT 'MGF' || lpad(year_2::text, 2, '0') || '-' || lpad(allocated_seq::text, 4, '0')
                FROM updated;
                """
            )
            .SingleAsync(cancellationToken);
    }

    private async Task AddProjectMemberAsync(string projectId, string personId, string roleKey, CancellationToken cancellationToken)
    {
        var projectMemberId = EntityIds.NewWithPrefix("prm");

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO public.project_members (project_member_id, project_id, person_id, role_key, assigned_at)
            VALUES ({projectMemberId}, {projectId}, {personId}, {roleKey}, now());
            """,
            cancellationToken
        );
    }
}


