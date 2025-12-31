namespace MGF.Api.Services;

using Microsoft.EntityFrameworkCore;
using MGF.Infrastructure.Data;

public sealed class ClientsService
{
    private readonly AppDbContext db;

    public ClientsService(AppDbContext db)
    {
        this.db = db;
    }

    public sealed record ClientDto(string ClientId, string DisplayName, string StatusKey, string ClientTypeKey);

    public async Task<IReadOnlyList<ClientDto>> GetClientsAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = db.Clients.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(c => c.StatusKey == "active" || c.StatusKey == "prospect");
        }

        return await query
            .OrderBy(c => c.DisplayName)
            .Select(c => new ClientDto(c.ClientId, c.DisplayName, c.StatusKey, c.ClientTypeKey))
            .ToListAsync(cancellationToken);
    }
}

