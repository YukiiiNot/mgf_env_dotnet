using MGF.Worker.ProjectBootstrap;
using Xunit;

namespace MGF.Worker.Tests;

public sealed class ProjectStorageRootSqlTests
{
    [Fact]
    public void UpsertUsesUniqueKeyConflict()
    {
        var sql = ProjectStorageRootSql.UpsertStorageRoot;

        Assert.Contains("ON CONFLICT (project_id, storage_provider_key, root_key)", sql);
    }
}
