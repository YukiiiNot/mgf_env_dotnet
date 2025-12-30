using MGF.Worker.ProjectArchive;

public sealed class ProjectArchiveGuardsTests
{
    [Fact]
    public void TryValidateStart_AllowsToArchive()
    {
        var allowed = ProjectArchiveGuards.TryValidateStart("to_archive", force: false, out var error, out var alreadyArchiving);

        Assert.True(allowed);
        Assert.False(alreadyArchiving);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateStart_AllowsArchiveFailed()
    {
        var allowed = ProjectArchiveGuards.TryValidateStart("archive_failed", force: false, out var error, out var alreadyArchiving);

        Assert.True(allowed);
        Assert.False(alreadyArchiving);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateStart_BlocksArchiving()
    {
        var allowed = ProjectArchiveGuards.TryValidateStart("archiving", force: false, out var error, out var alreadyArchiving);

        Assert.False(allowed);
        Assert.True(alreadyArchiving);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateStart_BlocksArchived()
    {
        var allowed = ProjectArchiveGuards.TryValidateStart("archived", force: false, out var error, out var alreadyArchiving);

        Assert.False(allowed);
        Assert.False(alreadyArchiving);
        Assert.NotNull(error);
    }
}
