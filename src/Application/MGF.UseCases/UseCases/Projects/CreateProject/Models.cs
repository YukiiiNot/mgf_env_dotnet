namespace MGF.UseCases.Projects.CreateProject;

public sealed record CreateProjectRequest(
    string ClientId,
    string ProjectName,
    string EditorPersonId,
    string TemplateKey);

public sealed record CreateProjectResult(
    string ProjectId,
    string JobId);
