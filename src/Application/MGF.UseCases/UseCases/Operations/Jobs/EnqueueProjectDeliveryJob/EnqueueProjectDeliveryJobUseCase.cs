namespace MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryJob;

using System.Text.Json;
using MGF.Contracts.Abstractions.Operations.Jobs;
using MGF.Domain.Entities;
using MGF.UseCases.Operations.Jobs;

public sealed class EnqueueProjectDeliveryJobUseCase : IEnqueueProjectDeliveryJobUseCase
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IJobOpsStore jobStore;

    public EnqueueProjectDeliveryJobUseCase(IJobOpsStore jobStore)
    {
        this.jobStore = jobStore;
    }

    public async Task<EnqueueProjectDeliveryJobResult> ExecuteAsync(
        EnqueueProjectDeliveryJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var existing = await jobStore.FindExistingJobAsync(
            "project.delivery",
            "project",
            request.ProjectId,
            cancellationToken);

        if (!DeliveryJobGuard.ShouldEnqueue(existing, out var reason))
        {
            return new EnqueueProjectDeliveryJobResult(
                Enqueued: false,
                Reason: reason,
                JobId: null,
                PayloadJson: null);
        }

        var payload = new ProjectDeliveryJobPayload(
            request.ProjectId,
            request.EditorInitials,
            request.ToEmails,
            request.ReplyToEmail,
            request.TestMode,
            request.AllowTestCleanup,
            request.AllowNonReal,
            request.Force,
            request.RefreshShareLink);

        var payloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        var jobId = EntityIds.NewWithPrefix("job");

        await jobStore.EnqueueRetryableJobAsync(
            new RetryableJobEnqueueRequest(
                JobId: jobId,
                JobTypeKey: "project.delivery",
                PayloadJson: payloadJson,
                EntityTypeKey: "project",
                EntityKey: request.ProjectId,
                AttemptCount: 0,
                MaxAttempts: 5),
            cancellationToken);

        return new EnqueueProjectDeliveryJobResult(
            Enqueued: true,
            Reason: null,
            JobId: jobId,
            PayloadJson: payloadJson);
    }

    private sealed record ProjectDeliveryJobPayload(
        string ProjectId,
        IReadOnlyList<string> EditorInitials,
        IReadOnlyList<string> ToEmails,
        string? ReplyToEmail,
        bool TestMode,
        bool AllowTestCleanup,
        bool AllowNonReal,
        bool Force,
        bool RefreshShareLink);
}
