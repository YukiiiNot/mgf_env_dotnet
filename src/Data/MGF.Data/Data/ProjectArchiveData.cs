namespace MGF.Data.Data;

using Microsoft.EntityFrameworkCore;
using MGF.Contracts.Abstractions.ProjectArchive;

public sealed class ProjectArchiveData : IProjectArchiveData
{
    private readonly AppDbContext db;

    public ProjectArchiveData(AppDbContext db)
    {
        this.db = db;
    }

    public async Task<ProjectArchivePathTemplates> GetArchivePathTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var keys = new[]
        {
            "dropbox_active_container_root",
            "dropbox_to_archive_container_root",
            "dropbox_archive_container_root",
            "nas_archive_root",
        };

        var templateRows = await db.Set<Dictionary<string, object>>("path_templates")
            .Where(row => keys.Contains(EF.Property<string>(row, "path_key")))
            .Select(row => new
            {
                Key = EF.Property<string>(row, "path_key"),
                Relpath = EF.Property<string>(row, "relpath"),
            })
            .ToListAsync(cancellationToken);

        string Resolve(string key, string fallback)
        {
            var match = templateRows.FirstOrDefault(row => string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match is null || string.IsNullOrWhiteSpace(match.Relpath))
            {
                return fallback;
            }

            return match.Relpath.Trim();
        }

        return new ProjectArchivePathTemplates(
            DropboxActiveRelpath: Resolve("dropbox_active_container_root", "02_Projects_Active"),
            DropboxToArchiveRelpath: Resolve("dropbox_to_archive_container_root", "03_Projects_ToArchive"),
            DropboxArchiveRelpath: Resolve("dropbox_archive_container_root", "98_Archive"),
            NasArchiveRelpath: Resolve("nas_archive_root", "01_Projects_Archive")
        );
    }
}
