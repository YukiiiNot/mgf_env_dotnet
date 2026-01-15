using MGF.Data.Stores.ProjectBootstrap;
using Xunit;

namespace MGF.Worker.Tests;

public sealed class ProjectStorageRootSqlTests
{
    [Fact]
    public void UpsertUsesUniqueKeyConflictAndSingleRow()
    {
        var sql = ProjectBootstrapSql.UpsertStorageRoot;

        Assert.Contains("ON CONFLICT (project_id, storage_provider_key, root_key)", sql);
        Assert.DoesNotContain("WITH", sql);
        Assert.DoesNotContain("UPDATE public.project_storage_roots", sql);
        Assert.Contains("VALUES", sql);
    }

    [Fact]
    public void UpdateIsPrimaryIsProviderScoped()
    {
        var sql = ProjectBootstrapSql.UpdateIsPrimaryForProvider;

        Assert.Contains("UPDATE public.project_storage_roots", sql);
        Assert.Contains("storage_provider_key = @storage_provider_key", sql);
        Assert.Contains("project_id = @project_id", sql);
    }

    [Fact]
    public void BuildUpdateStatusCommand_UsesExpectedArguments()
    {
        var command = ProjectBootstrapSql.BuildUpdateStatusCommand("proj_123", "active");

        Assert.Contains("UPDATE public.projects", command.Format);
        Assert.Contains("status_key", command.Format);
        Assert.Contains("updated_at = now()", command.Format);

        var args = command.GetArguments();
        Assert.Equal("active", args[0]);
        Assert.Equal("proj_123", args[1]);
    }
}
