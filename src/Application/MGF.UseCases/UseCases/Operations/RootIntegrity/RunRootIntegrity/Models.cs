namespace MGF.UseCases.Operations.RootIntegrity.RunRootIntegrity;

using MGF.Contracts.Abstractions.RootIntegrity;

public sealed record RunRootIntegrityRequest(
    RootIntegrityPayload Payload,
    string JobId);

public sealed record RunRootIntegrityResult(
    RootIntegrityResult Result);
