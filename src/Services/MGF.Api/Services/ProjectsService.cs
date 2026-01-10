namespace MGF.Api.Services;

using Microsoft.EntityFrameworkCore;
using MGF.Data.Data;
using MGF.UseCases.Projects.CreateProject;

public sealed class ProjectsService
{
    private const int MaxListLimit = 200;
    private readonly AppDbContext db;
    private readonly ICreateProjectUseCase createProjectUseCase;
    private readonly ILogger<ProjectsService> logger;

    public ProjectsService(
        ICreateProjectUseCase createProjectUseCase,
        ILogger<ProjectsService> logger,
        AppDbContext db)
    {
        this.createProjectUseCase = createProjectUseCase;
        this.logger = logger;
        this.db = db;
    }

    public sealed record CreateProjectRequest(string ClientId, string ProjectName, string EditorPersonId, string TemplateKey);

    public sealed record CreateProjectResponse(string ProjectId, string JobId);

    public sealed record ProjectListItemDto(
        string ProjectId,
        string ProjectCode,
        string ClientId,
        string Name,
        string StatusKey,
        string PhaseKey,
        string? PriorityKey,
        DateOnly? DueDate,
        DateTimeOffset? ArchivedAt,
        DateTimeOffset CreatedAt
    );

    public sealed record ProjectsListResponseDto(
        IReadOnlyList<ProjectListItemDto> Items,
        ProjectsListCursorDto? NextCursor
    );

    public sealed record ProjectsListCursorDto(
        DateTimeOffset CreatedAt,
        string ProjectId
    );

    public sealed record ProjectDetailDto(
        string ProjectId,
        string ProjectCode,
        string ClientId,
        string Name,
        string StatusKey,
        string PhaseKey,
        string? PriorityKey,
        DateOnly? DueDate,
        DateTimeOffset? ArchivedAt,
        string DataProfile,
        string? CurrentInvoiceId,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt
    );

    public async Task<CreateProjectResponse?> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await createProjectUseCase.ExecuteAsync(
            new MGF.UseCases.Projects.CreateProject.CreateProjectRequest(
                request.ClientId,
                request.ProjectName,
                request.EditorPersonId,
                request.TemplateKey),
            cancellationToken);

        if (result is null)
        {
            return null;
        }

        logger.LogInformation("MGF.Api: created project {ProjectId} and enqueued job {JobId}", result.ProjectId, result.JobId);

        return new CreateProjectResponse(result.ProjectId, result.JobId);
    }

    public async Task<ProjectsListResponseDto> GetProjectsAsync(
        int limit,
        DateTimeOffset? cursorCreatedAt,
        string? cursorProjectId,
        CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, MaxListLimit);

        var query = db.Projects.AsNoTracking();

        if (cursorCreatedAt.HasValue && !string.IsNullOrWhiteSpace(cursorProjectId))
        {
            var createdAt = cursorCreatedAt.Value;
            var projectId = cursorProjectId;
            query = query.Where(p =>
                p.CreatedAt < createdAt
                || (p.CreatedAt == createdAt && string.Compare(p.ProjectId, projectId) < 0));
        }

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.ProjectId)
            .Select(p => new ProjectListItemDto(
                p.ProjectId,
                p.ProjectCode,
                p.ClientId,
                p.Name,
                p.StatusKey,
                p.PhaseKey,
                p.PriorityKey,
                p.DueDate,
                p.ArchivedAt,
                p.CreatedAt
            ))
            .Take(boundedLimit)
            .ToListAsync(cancellationToken);

        ProjectsListCursorDto? nextCursor = null;
        if (items.Count == boundedLimit)
        {
            var last = items[^1];
            nextCursor = new ProjectsListCursorDto(last.CreatedAt, last.ProjectId);
        }

        return new ProjectsListResponseDto(items, nextCursor);
    }

    public async Task<ProjectDetailDto?> GetProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        return await db.Projects
            .AsNoTracking()
            .Where(p => p.ProjectId == projectId)
            .Select(p => new ProjectDetailDto(
                p.ProjectId,
                p.ProjectCode,
                p.ClientId,
                p.Name,
                p.StatusKey,
                p.PhaseKey,
                p.PriorityKey,
                p.DueDate,
                p.ArchivedAt,
                p.DataProfile,
                p.CurrentInvoiceId,
                p.CreatedAt,
                p.UpdatedAt
            ))
            .SingleOrDefaultAsync(cancellationToken);
    }
}


