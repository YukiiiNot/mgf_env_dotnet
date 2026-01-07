namespace MGF.UseCases.Operations.Jobs.EnqueueRootIntegrityJob;

using System.Text.Json;
using MGF.Contracts.Abstractions.Operations.Jobs;
using MGF.Domain.Entities;

public sealed class EnqueueRootIntegrityJobUseCase : IEnqueueRootIntegrityJobUseCase
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IJobOpsStore jobStore;

    public EnqueueRootIntegrityJobUseCase(IJobOpsStore jobStore)
    {
        this.jobStore = jobStore;
    }

    public async Task<EnqueueRootIntegrityJobResult> ExecuteAsync(
        EnqueueRootIntegrityJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        await jobStore.EnsureJobTypeAsync(
            "domain.root_integrity",
            "Domain: Root Integrity Check",
            cancellationToken);

        var payload = new RootIntegrityJobPayload(
            request.ProviderKey,
            request.RootKey,
            request.Mode,
            request.DryRun,
            request.QuarantineRelpath,
            request.MaxItems,
            request.MaxBytes);

        var payloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        var jobId = EntityIds.NewWithPrefix("job");

        await jobStore.EnqueueJobAsync(
            new JobEnqueueRequest(
                JobId: jobId,
                JobTypeKey: "domain.root_integrity",
                PayloadJson: payloadJson,
                EntityTypeKey: "storage_root",
                EntityKey: $"{request.ProviderKey}:{request.RootKey}"),
            cancellationToken);

        return new EnqueueRootIntegrityJobResult(jobId, payloadJson);
    }

    private sealed record RootIntegrityJobPayload(
        string ProviderKey,
        string RootKey,
        string Mode,
        bool DryRun,
        string? QuarantineRelpath,
        int? MaxItems,
        long? MaxBytes);
}
