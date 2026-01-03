namespace MGF.Data.Data;

using Microsoft.EntityFrameworkCore;
using MGF.Contracts.Abstractions.ProjectDelivery;

public sealed class ProjectDeliveryData : IProjectDeliveryData
{
    private readonly AppDbContext db;

    public ProjectDeliveryData(AppDbContext db)
    {
        this.db = db;
    }

    public async Task<string?> GetProjectStorageRootRelpathAsync(
        string projectId,
        string storageProviderKey,
        bool testMode,
        CancellationToken cancellationToken = default)
    {
        var rootKey = testMode ? "test_run" : "project_container";

        var relpath = await db.Set<Dictionary<string, object>>("project_storage_roots")
            .Where(row => EF.Property<string>(row, "project_id") == projectId)
            .Where(row => EF.Property<string>(row, "storage_provider_key") == storageProviderKey)
            .Where(row => EF.Property<string>(row, "root_key") == rootKey)
            .Select(row => EF.Property<string>(row, "folder_relpath"))
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(relpath) ? null : relpath;
    }

    public async Task<string> GetDropboxDeliveryRelpathAsync(
        CancellationToken cancellationToken = default)
    {
        var templateRow = await db.Set<Dictionary<string, object>>("path_templates")
            .Where(row => EF.Property<string>(row, "path_key") == "dropbox_delivery_root")
            .Select(row => EF.Property<string>(row, "relpath"))
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(templateRow) ? "04_Client_Deliveries" : templateRow.Trim();
    }
}
