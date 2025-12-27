using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using MGF.Tools.Provisioner;

var root = new RootCommand("MGF Folder Template Provisioner");

root.AddCommand(CreateCommand("plan", ProvisioningMode.Plan, includeForce: false));
root.AddCommand(CreateCommand("apply", ProvisioningMode.Apply, includeForce: false));
root.AddCommand(CreateCommand("verify", ProvisioningMode.Verify, includeForce: false));
root.AddCommand(CreateCommand("repair", ProvisioningMode.Repair, includeForce: true));

var parser = new CommandLineBuilder(root).UseDefaults().Build();
return await parser.InvokeAsync(args);

static Command CreateCommand(string name, ProvisioningMode mode, bool includeForce)
{
    var command = new Command(name, $"{name} a folder template (mode={mode}).");

    var templateOption = new Option<string>("--template")
    {
        Description = "Path to the folder template JSON file.",
        IsRequired = true
    };

    var baseOption = new Option<string>("--base")
    {
        Description = "Base directory where the project root folder will be created.",
        IsRequired = true
    };

    var schemaOption = new Option<string?>("--schema")
    {
        Description = "Optional path to the folder template JSON schema. Defaults to the template's $schema.",
        IsRequired = false
    };

    var seedsOption = new Option<string?>("--seeds")
    {
        Description = "Optional base directory for seed files referenced by SourceRelpath.",
        IsRequired = false
    };

    var projectCodeOption = new Option<string>("--projectCode")
    {
        Description = "Project code token (e.g., MGF25-0123).",
        IsRequired = true
    };

    var projectNameOption = new Option<string>("--projectName")
    {
        Description = "Project name token.",
        IsRequired = true
    };

    var clientNameOption = new Option<string?>("--clientName")
    {
        Description = "Client name token (optional).",
        IsRequired = false
    };

    var editorsOption = new Option<string[]>("--editors")
    {
        Description = "Comma-separated editor initials (e.g., EC,MM) or repeatable values.",
        Arity = ArgumentArity.ZeroOrMore
    };
    editorsOption.AllowMultipleArgumentsPerToken = true;

    var forceOption = new Option<bool>("--force")
    {
        Description = "Allow overwriting seeded files during repair (only).",
        IsRequired = false
    };

    command.AddOption(templateOption);
    command.AddOption(baseOption);
    command.AddOption(schemaOption);
    command.AddOption(seedsOption);
    command.AddOption(projectCodeOption);
    command.AddOption(projectNameOption);
    command.AddOption(clientNameOption);
    command.AddOption(editorsOption);

    if (includeForce)
    {
        command.AddOption(forceOption);
    }

    command.SetHandler(async context =>
    {
        var template = context.ParseResult.GetValueForOption(templateOption) ?? string.Empty;
        var basePath = context.ParseResult.GetValueForOption(baseOption) ?? string.Empty;
        var schema = context.ParseResult.GetValueForOption(schemaOption);
        var seeds = context.ParseResult.GetValueForOption(seedsOption);
        var projectCode = context.ParseResult.GetValueForOption(projectCodeOption) ?? string.Empty;
        var projectName = context.ParseResult.GetValueForOption(projectNameOption) ?? string.Empty;
        var clientName = context.ParseResult.GetValueForOption(clientNameOption);
        var editors = context.ParseResult.GetValueForOption(editorsOption) ?? Array.Empty<string>();
        var force = includeForce && context.ParseResult.GetValueForOption(forceOption);

        var exitCode = await RunAsync(mode, template, basePath, schema, seeds, projectCode, projectName, clientName, editors, force);
        context.ExitCode = exitCode;
    });

    return command;
}

static async Task<int> RunAsync(
    ProvisioningMode mode,
    string templatePath,
    string basePath,
    string? schemaPath,
    string? seedsPath,
    string projectCode,
    string projectName,
    string? clientName,
    string[] editors,
    bool force)
{
    try
    {
        var tokens = ProvisioningTokens.Create(projectCode, projectName, clientName, editors);
        var request = new ProvisioningRequest(
            Mode: mode,
            TemplatePath: templatePath,
            BasePath: basePath,
            SchemaPath: schemaPath,
            SeedsPath: seedsPath,
            Tokens: tokens,
            ForceOverwriteSeededFiles: force
        );

        var provisioner = new FolderProvisioner(new LocalFileStore());
        var result = await provisioner.ExecuteAsync(request, CancellationToken.None);

        result.WriteSummaryToConsole();
        return result.Success ? 0 : 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Provisioner failed: {ex.Message}");
        return 1;
    }
}
