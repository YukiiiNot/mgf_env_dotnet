namespace MGF.Api.Services;

using Microsoft.EntityFrameworkCore;
using MGF.Data.Data;

public sealed class PeopleService
{
    private readonly AppDbContext db;

    public PeopleService(AppDbContext db)
    {
        this.db = db;
    }

    public sealed record PersonDto(string PersonId, string FirstName, string LastName, string? DisplayName, string StatusKey);

    public async Task<IReadOnlyList<PersonDto>> GetPeopleAsync(string? roleKey, bool activeOnly, CancellationToken cancellationToken)
    {
        IQueryable<MGF.Domain.Entities.Person> query = db.People.AsNoTracking();

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
            .Select(p => new PersonDto(p.PersonId, p.FirstName, p.LastName, p.DisplayName, p.StatusKey))
            .ToListAsync(cancellationToken);
    }
}


