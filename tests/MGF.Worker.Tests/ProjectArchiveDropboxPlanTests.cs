using MGF.Worker.Adapters.Storage.ProjectArchive;

public sealed class ProjectArchiveDropboxPlanTests
{
    [Fact]
    public void BuildDropboxMovePlan_WhenArchiveExists_IsAlreadyArchived()
    {
        var plan = ProjectArchiveExecutor.BuildDropboxMovePlan(hasActive: true, hasToArchive: true, hasArchive: true);

        Assert.Equal("already_archived", plan.RootState);
        Assert.False(plan.ShouldMoveToArchive);
    }

    [Fact]
    public void BuildDropboxMovePlan_WhenToArchiveExists_IsReadyWithoutMove()
    {
        var plan = ProjectArchiveExecutor.BuildDropboxMovePlan(hasActive: true, hasToArchive: true, hasArchive: false);

        Assert.Equal("ready_to_archive", plan.RootState);
        Assert.False(plan.ShouldMoveToArchive);
    }

    [Fact]
    public void BuildDropboxMovePlan_WhenActiveExists_MovesToArchive()
    {
        var plan = ProjectArchiveExecutor.BuildDropboxMovePlan(hasActive: true, hasToArchive: false, hasArchive: false);

        Assert.Equal("ready_to_archive", plan.RootState);
        Assert.True(plan.ShouldMoveToArchive);
    }

    [Fact]
    public void BuildDropboxMovePlan_WhenMissing_ReturnsMissing()
    {
        var plan = ProjectArchiveExecutor.BuildDropboxMovePlan(hasActive: false, hasToArchive: false, hasArchive: false);

        Assert.Equal("container_missing", plan.RootState);
        Assert.False(plan.ShouldMoveToArchive);
    }
}
