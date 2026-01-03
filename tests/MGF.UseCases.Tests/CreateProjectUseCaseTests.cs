using System.Text.Json;
using MGF.Contracts.Abstractions.Projects;
using MGF.UseCases.Projects.CreateProject;

namespace MGF.UseCases.Tests;

public sealed class CreateProjectUseCaseTests
{
    [Theory]
    [InlineData("", "Project", "editor_1", "template")]
    [InlineData("client_1", "", "editor_1", "template")]
    [InlineData("client_1", "Project", "", "template")]
    [InlineData("client_1", "Project", "editor_1", "")]
    public async Task ExecuteAsync_ReturnsNull_WhenRequiredFieldsMissing(
        string clientId,
        string projectName,
        string editorPersonId,
        string templateKey)
    {
        var store = new FakeProjectCreationStore();
        var useCase = new CreateProjectUseCase(store);

        var result = await useCase.ExecuteAsync(new CreateProjectRequest(
            clientId,
            projectName,
            editorPersonId,
            templateKey));

        Assert.Null(result);
        Assert.Null(store.Command);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenClientMissing()
    {
        var store = new FakeProjectCreationStore { ClientExists = false };
        var useCase = new CreateProjectUseCase(store);

        var result = await useCase.ExecuteAsync(new CreateProjectRequest(
            "client_1",
            "Project",
            "editor_1",
            "template"));

        Assert.Null(result);
        Assert.Null(store.Command);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenEditorMissing()
    {
        var store = new FakeProjectCreationStore { PersonExists = false };
        var useCase = new CreateProjectUseCase(store);

        var result = await useCase.ExecuteAsync(new CreateProjectRequest(
            "client_1",
            "Project",
            "editor_1",
            "template"));

        Assert.Null(result);
        Assert.Null(store.Command);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenEditorRoleMissing()
    {
        var store = new FakeProjectCreationStore { IsEditor = false };
        var useCase = new CreateProjectUseCase(store);

        var result = await useCase.ExecuteAsync(new CreateProjectRequest(
            "client_1",
            "Project",
            "editor_1",
            "template"));

        Assert.Null(result);
        Assert.Null(store.Command);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesProject_WithPayloadAndRoles()
    {
        var store = new FakeProjectCreationStore
        {
            Roles = new[] { "producer", "ADMIN", "viewer" }
        };
        var useCase = new CreateProjectUseCase(store);

        var result = await useCase.ExecuteAsync(new CreateProjectRequest(
            "client_1",
            "Project",
            "editor_1",
            "template"));

        Assert.NotNull(result);
        Assert.NotNull(store.Command);
        Assert.Equal(result!.ProjectId, store.Command!.ProjectId);
        Assert.Equal(result.JobId, store.Command.JobId);
        Assert.Equal("client_1", store.Command.ClientId);
        Assert.Equal("Project", store.Command.ProjectName);
        Assert.Equal("editor_1", store.Command.EditorPersonId);
        Assert.Equal(new[] { "producer", "ADMIN" }, store.Command.AdditionalRoleKeys);

        using var payload = JsonDocument.Parse(store.Command.PayloadJson);
        var root = payload.RootElement;
        Assert.Equal(store.Command.ProjectId, root.GetProperty("projectId").GetString());
        Assert.Equal("client_1", root.GetProperty("clientId").GetString());
        Assert.Equal("template", root.GetProperty("templateKey").GetString());
    }

    private sealed class FakeProjectCreationStore : IProjectCreationStore
    {
        public bool ClientExists { get; set; } = true;
        public bool PersonExists { get; set; } = true;
        public bool IsEditor { get; set; } = true;
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
        public CreateProjectCommand? Command { get; private set; }

        public Task<bool> ClientExistsAsync(string clientId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ClientExists);
        }

        public Task<bool> PersonExistsAsync(string personId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PersonExists);
        }

        public Task<bool> PersonHasRoleAsync(
            string personId,
            string roleKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsEditor && roleKey == "editor");
        }

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(
            string personId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Roles);
        }

        public Task CreateProjectAsync(CreateProjectCommand command, CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.CompletedTask;
        }
    }
}
