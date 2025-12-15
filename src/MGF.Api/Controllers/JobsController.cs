namespace MGF.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using MGF.Api.Services;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController : ControllerBase
{
    private readonly JobsService jobs;

    public JobsController(JobsService jobs)
    {
        this.jobs = jobs;
    }

    [HttpGet("{jobId}")]
    public async Task<ActionResult<JobsService.JobDto>> GetJob(string jobId, CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetJobAsync(jobId, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }
}

