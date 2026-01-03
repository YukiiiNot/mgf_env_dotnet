namespace MGF.UseCases.Operations.ProjectArchive.RunProjectArchive;

using MGF.Contracts.Abstractions.ProjectArchive;

public sealed record RunProjectArchiveRequest(
    ProjectArchivePayload Payload,
    string JobId
);

public sealed record RunProjectArchiveResult(
    ProjectArchiveRunResult Result
);
