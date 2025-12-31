using Microsoft.EntityFrameworkCore;
using MGF.Contracts.Abstractions;
using MGF.Domain.Entities;

namespace MGF.Infrastructure.Data.Repositories;

public sealed class EfProjectRepository : IProjectRepository
{
    private readonly AppDbContext _db;

    public EfProjectRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Project?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default)
    {
        return _db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);
    }

    public async Task SaveAsync(Project project, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Projects.AsNoTracking()
            .AnyAsync(x => x.ProjectId == project.ProjectId, cancellationToken);

        if (exists)
        {
            _db.Projects.Update(project);
        }
        else
        {
            await _db.Projects.AddAsync(project, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

