using MGF.Contracts.Abstractions.ProjectArchive;

public sealed class ProjectArchivePayloadTests
{
    [Fact]
    public void ParsePayload_RequiresProjectId()
    {
        var json = """{"testMode":true}""";

        var ex = Assert.Throws<InvalidOperationException>(() => ProjectArchivePayload.Parse(json));
        Assert.Contains("projectId is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParsePayload_ParsesEditorInitials()
    {
        var json = """{"projectId":"prj_test","editorInitials":["ER","AB"]}""";

        var payload = ProjectArchivePayload.Parse(json);

        Assert.Equal("prj_test", payload.ProjectId);
        Assert.Equal(new[] { "ER", "AB" }, payload.EditorInitials);
    }
}
