namespace MGF.Contracts.Abstractions.Operations.Projects;

public interface IProjectContactOpsStore
{
    Task<PrimaryContactEmailResult?> EnsurePrimaryContactEmailAsync(
        string clientId,
        string email,
        CancellationToken cancellationToken = default);
}

public sealed record PrimaryContactEmailResult(
    string ClientId,
    string PersonId,
    string Email,
    bool Inserted,
    bool Updated);
