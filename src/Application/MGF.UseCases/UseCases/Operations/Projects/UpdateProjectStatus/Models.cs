namespace MGF.UseCases.Operations.Projects.UpdateProjectStatus;

public sealed record UpdateProjectStatusRequest(
    string ProjectId,
    string StatusKey);

public sealed record UpdateProjectStatusResult(
    int RowsAffected);
