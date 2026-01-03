namespace MGF.UseCases.Operations.People.ListPeople;

public interface IListPeopleUseCase
{
    Task<ListPeopleResult> ExecuteAsync(
        ListPeopleRequest request,
        CancellationToken cancellationToken = default);
}
