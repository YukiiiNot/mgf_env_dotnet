using MGF.Data.Stores.Projects;

namespace MGF.Worker.Tests;

public sealed class ProjectCreationSqlTests
{
    [Fact]
    public void BuildInsertProjectMemberCommand_UsesExpectedArguments()
    {
        var command = ProjectCreationSql.BuildInsertProjectMemberCommand(
            "prm_123",
            "prj_123",
            "per_123",
            "editor");

        Assert.Contains("INSERT INTO public.project_members", command.Format);
        Assert.Contains("assigned_at", command.Format);

        var args = command.GetArguments();
        Assert.Equal("prm_123", args[0]);
        Assert.Equal("prj_123", args[1]);
        Assert.Equal("per_123", args[2]);
        Assert.Equal("editor", args[3]);
    }

    [Fact]
    public void BuildEnqueueProjectCreationJobCommand_UsesExpectedArguments()
    {
        var command = ProjectCreationSql.BuildEnqueueProjectCreationJobCommand(
            "job_123",
            "{\"projectId\":\"prj_123\"}",
            "prj_123");

        Assert.Contains("INSERT INTO public.jobs", command.Format);
        Assert.Contains("job_type_key", command.Format);
        Assert.Contains("payload", command.Format);

        var args = command.GetArguments();
        Assert.Equal("job_123", args[0]);
        Assert.Equal("dropbox.create_project_structure", args[1]);
        Assert.Equal("{\"projectId\":\"prj_123\"}", args[2]);
        Assert.Equal("queued", args[3]);
        Assert.Equal("project", args[4]);
        Assert.Equal("prj_123", args[5]);
    }
}
