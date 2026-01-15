namespace MGF.UseCases.Operations.Jobs.EnqueueProjectArchiveJob;

using System.Text.Json;
using MGF.Contracts.Abstractions.Operations.Jobs;
using MGF.Domain.Entities;
using MGF.UseCases.Operations.Jobs;

public sealed class EnqueueProjectArchiveJobUseCase : IEnqueueProjectArchiveJobUseCase
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IJobOpsStore jobStore;

    public EnqueueProjectArchiveJobUseCase(IJobOpsStore jobStore)
    {
        this.jobStore = jobStore;
    }

    public async Task<EnqueueProjectArchiveJobResult> ExecuteAsync(
        EnqueueProjectArchiveJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        await jobStore.EnsureJobTypeAsync("project.archive", "Project: Archive", cancellationToken);

        var existing = await jobStore.FindExistingJobAsync(
            "project.archive",
            "project",
            request.ProjectId,
            cancellationToken);

        if (!ArchiveJobGuard.ShouldEnqueue(existing, out var reason))
        {
            return new EnqueueProjectArchiveJobResult(
                Enqueued: false,
                Reason: reason,
                JobId: null,
                PayloadJson: null);
        }

        var payload = new ProjectArchiveJobPayload(
            request.ProjectId,
            request.EditorInitials,
            request.TestMode,
            request.AllowTestCleanup,
            request.AllowNonReal,
            request.Force);

        var payloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        var jobId = EntityIds.NewWithPrefix("job");

        await jobStore.EnqueueJobAsync(
            new JobEnqueueRequest(
                JobId: jobId,
                JobTypeKey: "project.archive",
                PayloadJson: payloadJson,
                EntityTypeKey: "project",
                EntityKey: request.ProjectId),
            cancellationToken);

        return new EnqueueProjectArchiveJobResult(
            Enqueued: true,
            Reason: null,
            JobId: jobId,
            PayloadJson: payloadJson);
    }

    private sealed record ProjectArchiveJobPayload(
        string ProjectId,
        IReadOnlyList<string> EditorInitials,
        bool TestMode,
        bool AllowTestCleanup,
        bool AllowNonReal,
        bool Force);
}
