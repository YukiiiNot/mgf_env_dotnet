namespace MGF.UseCases.Operations.Jobs.EnqueueProjectBootstrapJob;

using System.Text.Json;
using MGF.Contracts.Abstractions.Operations.Jobs;
using MGF.Domain.Entities;
using MGF.UseCases.Operations.Jobs;

public sealed class EnqueueProjectBootstrapJobUseCase : IEnqueueProjectBootstrapJobUseCase
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IJobOpsStore jobStore;

    public EnqueueProjectBootstrapJobUseCase(IJobOpsStore jobStore)
    {
        this.jobStore = jobStore;
    }

    public async Task<EnqueueProjectBootstrapJobResult> ExecuteAsync(
        EnqueueProjectBootstrapJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        await jobStore.EnsureJobTypeAsync("project.bootstrap", "Project: Bootstrap", cancellationToken);

        var existing = await jobStore.FindExistingJobAsync(
            "project.bootstrap",
            "project",
            request.ProjectId,
            cancellationToken);

        if (!BootstrapJobGuard.ShouldEnqueue(existing, out var reason))
        {
            return new EnqueueProjectBootstrapJobResult(
                Enqueued: false,
                Reason: reason,
                JobId: null,
                PayloadJson: null);
        }

        var payload = new ProjectBootstrapJobPayload(
            request.ProjectId,
            request.EditorInitials,
            request.VerifyDomainRoots,
            request.CreateDomainRoots,
            request.ProvisionProjectContainers,
            request.AllowRepair,
            request.ForceSandbox,
            request.AllowNonReal,
            request.Force,
            request.TestMode,
            request.AllowTestCleanup);

        var payloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        var jobId = EntityIds.NewWithPrefix("job");

        await jobStore.EnqueueJobAsync(
            new JobEnqueueRequest(
                JobId: jobId,
                JobTypeKey: "project.bootstrap",
                PayloadJson: payloadJson,
                EntityTypeKey: "project",
                EntityKey: request.ProjectId),
            cancellationToken);

        return new EnqueueProjectBootstrapJobResult(
            Enqueued: true,
            Reason: null,
            JobId: jobId,
            PayloadJson: payloadJson);
    }

    private sealed record ProjectBootstrapJobPayload(
        string ProjectId,
        IReadOnlyList<string> EditorInitials,
        bool VerifyDomainRoots,
        bool CreateDomainRoots,
        bool ProvisionProjectContainers,
        bool AllowRepair,
        bool ForceSandbox,
        bool AllowNonReal,
        bool Force,
        bool TestMode,
        bool AllowTestCleanup);
}
