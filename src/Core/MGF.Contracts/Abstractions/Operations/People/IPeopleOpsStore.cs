namespace MGF.Contracts.Abstractions.Operations.People;

public interface IPeopleOpsStore
{
    Task<IReadOnlyList<PersonListItem>> ListPeopleAsync(
        string? roleKey,
        bool activeOnly,
        CancellationToken cancellationToken = default);
}

public sealed record PersonListItem(
    string PersonId,
    string FirstName,
    string LastName,
    string? DisplayName,
    string StatusKey);
