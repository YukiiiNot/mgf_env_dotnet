namespace MGF.Tools.Provisioner.Tests;

internal static class TestTemplates
{
    public static FolderTemplate CreateTemplate(params FolderNode[] children)
    {
        return new FolderTemplate
        {
            TemplateKey = "test_template",
            Root = new FolderNode
            {
                Name = "{PROJECT_CODE}_{PROJECT_NAME}",
                Children = children.ToList()
            }
        };
    }
}
