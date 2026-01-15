namespace MGF.Api.Controllers;

using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using MGF.Api.Services;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly ProjectsService projects;

    public ProjectsController(ProjectsService projects)
    {
        this.projects = projects;
    }

    [HttpGet]
    public async Task<ActionResult<ProjectsService.ProjectsListResponseDto>> GetProjects(
        [FromQuery] string? since = null,
        [FromQuery] string? limit = null,
        [FromQuery] string? cursorCreatedAt = null,
        [FromQuery] string? cursorProjectId = null,
        CancellationToken cancellationToken = default)
    {
        var parsedSince = DateTimeOffset.UtcNow.AddHours(-24);
        if (!string.IsNullOrWhiteSpace(since)
            && !DateTimeOffset.TryParse(
                since,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsedSince))
        {
            return BadRequest("since must be an ISO 8601 datetime.");
        }

        var parsedLimit = 200;
        if (!string.IsNullOrWhiteSpace(limit)
            && !int.TryParse(limit, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedLimit))
        {
            return BadRequest("limit must be an integer between 1 and 200.");
        }

        if (parsedLimit < 1 || parsedLimit > 200)
        {
            return BadRequest("limit must be between 1 and 200.");
        }

        DateTimeOffset? parsedCursorCreatedAt = null;
        string? parsedCursorProjectId = null;
        if (!string.IsNullOrWhiteSpace(cursorCreatedAt) || !string.IsNullOrWhiteSpace(cursorProjectId))
        {
            if (string.IsNullOrWhiteSpace(cursorCreatedAt) || string.IsNullOrWhiteSpace(cursorProjectId))
            {
                return BadRequest("cursorCreatedAt and cursorProjectId must be provided together.");
            }

            if (!DateTimeOffset.TryParse(
                    cursorCreatedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var cursorTime))
            {
                return BadRequest("cursorCreatedAt must be an ISO 8601 datetime.");
            }

            parsedCursorProjectId = cursorProjectId.Trim();
            if (string.IsNullOrWhiteSpace(parsedCursorProjectId))
            {
                return BadRequest("cursorProjectId must not be empty.");
            }

            parsedCursorCreatedAt = cursorTime;
        }

        var result = await projects.GetProjectsAsync(
            parsedSince,
            parsedLimit,
            parsedCursorCreatedAt,
            parsedCursorProjectId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{projectId}")]
    public async Task<ActionResult<ProjectsService.ProjectDetailDto>> GetProject(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await projects.GetProjectAsync(projectId, cancellationToken);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectsService.CreateProjectResponse>> CreateProject(
        [FromBody] ProjectsService.CreateProjectRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = await projects.CreateProjectAsync(request, cancellationToken);
        return result is null ? BadRequest() : Ok(result);
    }
}

