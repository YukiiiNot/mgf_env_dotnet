namespace MGF.UseCases.Operations.People.ListPeople;

using MGF.Contracts.Abstractions.Operations.People;

public sealed record ListPeopleRequest(
    string? RoleKey,
    bool ActiveOnly);

public sealed record ListPeopleResult(
    IReadOnlyList<PersonListItem> People);
