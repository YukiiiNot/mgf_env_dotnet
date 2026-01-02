namespace MGF.Api.Services;

using MGF.UseCases.Operations.People.ListPeople;

public sealed class PeopleService
{
    private readonly IListPeopleUseCase listPeopleUseCase;

    public PeopleService(IListPeopleUseCase listPeopleUseCase)
    {
        this.listPeopleUseCase = listPeopleUseCase;
    }

    public sealed record PersonDto(string PersonId, string FirstName, string LastName, string? DisplayName, string StatusKey);

    public async Task<IReadOnlyList<PersonDto>> GetPeopleAsync(string? roleKey, bool activeOnly, CancellationToken cancellationToken)
    {
        var result = await listPeopleUseCase.ExecuteAsync(
            new ListPeopleRequest(roleKey, activeOnly),
            cancellationToken);

        return result.People
            .Select(p => new PersonDto(p.PersonId, p.FirstName, p.LastName, p.DisplayName, p.StatusKey))
            .ToList();
    }
}


