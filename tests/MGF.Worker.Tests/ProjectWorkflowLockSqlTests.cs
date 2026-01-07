namespace MGF.Worker.Tests;

using MGF.Contracts.Abstractions.ProjectWorkflows;
using MGF.Data.Stores.ProjectWorkflows;

public sealed class ProjectWorkflowLockSqlTests
{
    [Fact]
    public void SqlCommands_UseAdvisoryLockFunctions()
    {
        Assert.Contains("pg_try_advisory_lock", ProjectWorkflowLockSql.TryAcquire);
        Assert.Contains("pg_advisory_unlock", ProjectWorkflowLockSql.Release);
    }

    [Fact]
    public void LockKey_IsStableAcrossCalls()
    {
        var first = ProjectWorkflowLockKey.Build(StorageMutationScopes.ForProject("prj_1"), ProjectWorkflowKinds.StorageMutation);
        var second = ProjectWorkflowLockKey.Build(StorageMutationScopes.ForProject("prj_1"), ProjectWorkflowKinds.StorageMutation);
        var other = ProjectWorkflowLockKey.Build(StorageMutationScopes.ForProject("prj_2"), ProjectWorkflowKinds.StorageMutation);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }
}
