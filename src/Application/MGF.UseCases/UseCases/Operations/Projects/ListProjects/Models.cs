namespace MGF.UseCases.Operations.Projects.ListProjects;

using MGF.Contracts.Abstractions.Operations.Projects;

public sealed record ListProjectsRequest(
    int Limit);

public sealed record ListProjectsResult(
    IReadOnlyList<ProjectListItem> Projects);
