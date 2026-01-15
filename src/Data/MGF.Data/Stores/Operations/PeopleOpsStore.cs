namespace MGF.Data.Stores.Operations;

using Microsoft.EntityFrameworkCore;
using MGF.Contracts.Abstractions.Operations.People;
using MGF.Data.Data;
using MGF.Domain.Entities;

public sealed class PeopleOpsStore : IPeopleOpsStore
{
    private readonly AppDbContext db;

    public PeopleOpsStore(AppDbContext db)
    {
        this.db = db;
    }

    public async Task<IReadOnlyList<PersonListItem>> ListPeopleAsync(
        string? roleKey,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Person> query = db.People.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(p => p.StatusKey == "active");
        }

        if (!string.IsNullOrWhiteSpace(roleKey))
        {
            var roles = db.Set<Dictionary<string, object>>("person_roles").AsNoTracking();

            query =
                from person in query
                join personRole in roles on person.PersonId equals EF.Property<string>(personRole, "person_id")
                where EF.Property<string>(personRole, "role_key") == roleKey
                select person;
        }

        return await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Select(p => new PersonListItem(p.PersonId, p.FirstName, p.LastName, p.DisplayName, p.StatusKey))
            .ToListAsync(cancellationToken);
    }
}
