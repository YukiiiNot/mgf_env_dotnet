namespace MGF.Api.Controllers;

using System.Globalization;
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

    [HttpGet]
    public async Task<ActionResult<JobsService.JobsListResponseDto>> GetJobs(
        [FromQuery] string? since = null,
        [FromQuery] string? limit = null,
        [FromQuery] string? cursorCreatedAt = null,
        [FromQuery] string? cursorJobId = null,
        [FromQuery] string? statusKey = null,
        [FromQuery] string? jobTypeKey = null,
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
        string? parsedCursorJobId = null;
        if (!string.IsNullOrWhiteSpace(cursorCreatedAt) || !string.IsNullOrWhiteSpace(cursorJobId))
        {
            if (string.IsNullOrWhiteSpace(cursorCreatedAt) || string.IsNullOrWhiteSpace(cursorJobId))
            {
                return BadRequest("cursorCreatedAt and cursorJobId must be provided together.");
            }

            if (!DateTimeOffset.TryParse(
                    cursorCreatedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var cursorTime))
            {
                return BadRequest("cursorCreatedAt must be an ISO 8601 datetime.");
            }

            parsedCursorJobId = cursorJobId.Trim();
            if (string.IsNullOrWhiteSpace(parsedCursorJobId))
            {
                return BadRequest("cursorJobId must not be empty.");
            }

            parsedCursorCreatedAt = cursorTime;
        }

        if (statusKey is not null)
        {
            statusKey = statusKey.Trim();
            if (string.IsNullOrWhiteSpace(statusKey))
            {
                return BadRequest("statusKey must not be empty.");
            }
        }

        if (jobTypeKey is not null)
        {
            jobTypeKey = jobTypeKey.Trim();
            if (string.IsNullOrWhiteSpace(jobTypeKey))
            {
                return BadRequest("jobTypeKey must not be empty.");
            }
        }

        var result = await jobs.GetJobsAsync(
            parsedSince,
            parsedLimit,
            parsedCursorCreatedAt,
            parsedCursorJobId,
            statusKey,
            jobTypeKey,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{jobId}")]
    public async Task<ActionResult<JobsService.JobDto>> GetJob(string jobId, CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetJobAsync(jobId, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }
}

