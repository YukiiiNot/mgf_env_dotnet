using System.Text;

namespace MGF.Tools.Provisioner;

public static class ContentTemplates
{
    public static bool TryGenerate(string key, ProvisioningTokens tokens, out byte[] content)
    {
        if (string.Equals(key, "readme-start-here", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Project Starter");
            builder.AppendLine();
            builder.AppendLine($"Project Code: {tokens.ProjectCode}");
            builder.AppendLine($"Project Name: {tokens.ProjectName}");
            if (!string.IsNullOrWhiteSpace(tokens.ClientName))
            {
                builder.AppendLine($"Client: {tokens.ClientName}");
            }

            if (tokens.EditorInitials.Count > 0)
            {
                builder.AppendLine($"Editors: {string.Join(", ", tokens.EditorInitials)}");
            }

            builder.AppendLine();
            builder.AppendLine("This folder structure was created by MGF.Tools.Provisioner.");
            builder.AppendLine("Place additional notes for the team here.");

            content = Encoding.UTF8.GetBytes(builder.ToString());
            return true;
        }

        content = Array.Empty<byte>();
        return false;
    }
}
