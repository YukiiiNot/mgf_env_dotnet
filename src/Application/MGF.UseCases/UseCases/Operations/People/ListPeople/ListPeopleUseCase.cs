namespace MGF.UseCases.Operations.People.ListPeople;

using MGF.Contracts.Abstractions.Operations.People;

public sealed class ListPeopleUseCase : IListPeopleUseCase
{
    private readonly IPeopleOpsStore peopleStore;

    public ListPeopleUseCase(IPeopleOpsStore peopleStore)
    {
        this.peopleStore = peopleStore;
    }

    public async Task<ListPeopleResult> ExecuteAsync(
        ListPeopleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var people = await peopleStore.ListPeopleAsync(request.RoleKey, request.ActiveOnly, cancellationToken);
        return new ListPeopleResult(people);
    }
}
