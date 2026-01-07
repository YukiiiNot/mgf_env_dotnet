namespace MGF.Api.Controllers;

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

