namespace MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryEmailJob;

using System.Text.Json;
using MGF.Contracts.Abstractions.Operations.Jobs;
using MGF.Domain.Entities;
using MGF.UseCases.Operations.Jobs;

public sealed class EnqueueProjectDeliveryEmailJobUseCase : IEnqueueProjectDeliveryEmailJobUseCase
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IJobOpsStore jobStore;

    public EnqueueProjectDeliveryEmailJobUseCase(IJobOpsStore jobStore)
    {
        this.jobStore = jobStore;
    }

    public async Task<EnqueueProjectDeliveryEmailJobResult> ExecuteAsync(
        EnqueueProjectDeliveryEmailJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var existing = await jobStore.FindExistingJobAsync(
            "project.delivery_email",
            "project",
            request.ProjectId,
            cancellationToken);

        if (!DeliveryEmailJobGuard.ShouldEnqueue(existing, out var reason))
        {
            return new EnqueueProjectDeliveryEmailJobResult(
                Enqueued: false,
                Reason: reason,
                JobId: null,
                PayloadJson: null);
        }

        var payload = new ProjectDeliveryEmailJobPayload(
            request.ProjectId,
            request.EditorInitials,
            request.ToEmails,
            request.ReplyToEmail);

        var payloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        var jobId = EntityIds.NewWithPrefix("job");

        await jobStore.EnqueueRetryableJobAsync(
            new RetryableJobEnqueueRequest(
                JobId: jobId,
                JobTypeKey: "project.delivery_email",
                PayloadJson: payloadJson,
                EntityTypeKey: "project",
                EntityKey: request.ProjectId,
                AttemptCount: 0,
                MaxAttempts: 5),
            cancellationToken);

        return new EnqueueProjectDeliveryEmailJobResult(
            Enqueued: true,
            Reason: null,
            JobId: jobId,
            PayloadJson: payloadJson);
    }

    private sealed record ProjectDeliveryEmailJobPayload(
        string ProjectId,
        IReadOnlyList<string> EditorInitials,
        IReadOnlyList<string> ToEmails,
        string? ReplyToEmail);
}
