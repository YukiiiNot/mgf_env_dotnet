namespace MGF.UseCases.Operations.Projects.CreateTestProject;

using MGF.Contracts.Abstractions.Operations.Projects;

public sealed record CreateTestProjectRequest(
    string TestKey,
    string ClientName,
    string ProjectName,
    string EditorFirstName,
    string EditorLastName,
    string EditorInitials,
    bool ForceNew);

public sealed record CreateTestProjectResult(
    bool Created,
    TestProjectInfo? ExistingProject,
    CreatedTestProject? CreatedProject);
