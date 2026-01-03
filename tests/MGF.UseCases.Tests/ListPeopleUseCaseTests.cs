using MGF.Contracts.Abstractions.Operations.People;
using MGF.UseCases.Operations.People.ListPeople;

namespace MGF.UseCases.Tests;

public sealed class ListPeopleUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsStoreResults()
    {
        var store = new FakePeopleStore
        {
            People = new[]
            {
                new PersonListItem("per_1", "Ada", "Lovelace", "Ada Lovelace", "active")
            }
        };
        var useCase = new ListPeopleUseCase(store);

        var result = await useCase.ExecuteAsync(new ListPeopleRequest(RoleKey: null, ActiveOnly: false));

        Assert.Single(result.People);
        Assert.Equal("per_1", result.People[0].PersonId);
    }

    [Fact]
    public async Task ExecuteAsync_PassesFiltersToStore()
    {
        var store = new FakePeopleStore();
        var useCase = new ListPeopleUseCase(store);

        await useCase.ExecuteAsync(new ListPeopleRequest(RoleKey: "editor", ActiveOnly: true));

        Assert.Equal("editor", store.LastRoleKey);
        Assert.True(store.LastActiveOnly);
    }

    private sealed class FakePeopleStore : IPeopleOpsStore
    {
        public IReadOnlyList<PersonListItem> People { get; set; } = Array.Empty<PersonListItem>();
        public string? LastRoleKey { get; private set; }
        public bool LastActiveOnly { get; private set; }

        public Task<IReadOnlyList<PersonListItem>> ListPeopleAsync(
            string? roleKey,
            bool activeOnly,
            CancellationToken cancellationToken = default)
        {
            LastRoleKey = roleKey;
            LastActiveOnly = activeOnly;
            return Task.FromResult(People);
        }
    }
}
