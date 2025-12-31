using Microsoft.EntityFrameworkCore;
using MGF.Contracts.Abstractions;
using MGF.Domain.Entities;

namespace MGF.Infrastructure.Data.Repositories;

public sealed class EfPersonRepository : IPersonRepository
{
    private readonly AppDbContext _db;

    public EfPersonRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Person?> GetByIdAsync(string personId, CancellationToken cancellationToken = default)
    {
        return _db.People.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == personId, cancellationToken);
    }

    public async Task SaveAsync(Person person, CancellationToken cancellationToken = default)
    {
        var exists = await _db.People.AsNoTracking()
            .AnyAsync(x => x.PersonId == person.PersonId, cancellationToken);

        if (exists)
        {
            _db.People.Update(person);
        }
        else
        {
            await _db.People.AddAsync(person, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

