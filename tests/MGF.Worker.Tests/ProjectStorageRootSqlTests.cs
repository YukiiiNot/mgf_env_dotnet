using MGF.Worker.ProjectBootstrap;
using Xunit;

namespace MGF.Worker.Tests;

public sealed class ProjectStorageRootSqlTests
{
    [Fact]
    public void UpsertUsesUniqueKeyConflictAndSingleRow()
    {
        var sql = ProjectStorageRootSql.UpsertStorageRoot;

        Assert.Contains("ON CONFLICT (project_id, storage_provider_key, root_key)", sql);
        Assert.DoesNotContain("WITH", sql);
        Assert.DoesNotContain("UPDATE public.project_storage_roots", sql);
        Assert.Contains("VALUES", sql);
    }

    [Fact]
    public void UpdateIsPrimaryIsProviderScoped()
    {
        var sql = ProjectStorageRootSql.UpdateIsPrimaryForProvider;

        Assert.Contains("UPDATE public.project_storage_roots", sql);
        Assert.Contains("storage_provider_key = @storage_provider_key", sql);
        Assert.Contains("project_id = @project_id", sql);
    }
}
