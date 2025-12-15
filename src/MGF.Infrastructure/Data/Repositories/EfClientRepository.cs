using Microsoft.EntityFrameworkCore;
using MGF.Application.Abstractions;
using MGF.Domain.Entities;

namespace MGF.Infrastructure.Data.Repositories;

public sealed class EfClientRepository : IClientRepository
{
    private readonly AppDbContext _db;

    public EfClientRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Client?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        return _db.Clients.AsNoTracking().SingleOrDefaultAsync(x => x.CliId == clientId, cancellationToken);
    }

    public async Task SaveAsync(Client client, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Clients.AsNoTracking()
            .AnyAsync(x => x.CliId == client.CliId, cancellationToken);

        if (exists)
        {
            _db.Clients.Update(client);
        }
        else
        {
            await _db.Clients.AddAsync(client, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
