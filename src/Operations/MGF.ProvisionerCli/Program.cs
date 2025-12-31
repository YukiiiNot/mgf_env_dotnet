using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using MGF.Tools.Provisioner;

var root = new RootCommand("MGF Folder Template Provisioner");

root.AddCommand(CreateProvisionCommand("plan", ProvisioningMode.Plan, includeForce: false));
root.AddCommand(CreateProvisionCommand("apply", ProvisioningMode.Apply, includeForce: false));
root.AddCommand(CreateProvisionCommand("verify", ProvisioningMode.Verify, includeForce: false));
root.AddCommand(CreateProvisionCommand("repair", ProvisioningMode.Repair, includeForce: true));
root.AddCommand(CreateValidateCommand());

var parser = new CommandLineBuilder(root).UseDefaults().Build();
return await parser.InvokeAsync(args);

static Command CreateProvisionCommand(string name, ProvisioningMode mode, bool includeForce)
{
    var command = new Command(name, $"{name} a folder template (mode={mode}).");

    var templateOption = new Option<string>("--template")
    {
        Description = "Path to the folder template JSON file.",
        IsRequired = true
    };

    var baseOption = new Option<string?>("--base")
    {
        Description = "Base directory where the project root folder will be created. Defaults to .\\runtime\\provisioner_runs.",
        IsRequired = false
    };

    var schemaOption = new Option<string?>("--schema")
    {
        Description = "Optional path to the folder template JSON schema. Defaults to .\\artifacts\\schemas\\mgf.folderTemplate.schema.json.",
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
        var basePath = context.ParseResult.GetValueForOption(baseOption);
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

static Command CreateValidateCommand()
{
    var command = new Command("validate", "validate a folder template against the schema.");

    var templateOption = new Option<string>("--template")
    {
        Description = "Path to the folder template JSON file.",
        IsRequired = true
    };

    var schemaOption = new Option<string?>("--schema")
    {
        Description = "Optional path to the folder template JSON schema. Defaults to .\\artifacts\\schemas\\mgf.folderTemplate.schema.json.",
        IsRequired = false
    };

    command.AddOption(templateOption);
    command.AddOption(schemaOption);

    command.SetHandler(async context =>
    {
        var template = context.ParseResult.GetValueForOption(templateOption) ?? string.Empty;
        var schema = context.ParseResult.GetValueForOption(schemaOption);

        var exitCode = await ValidateAsync(template, schema);
        context.ExitCode = exitCode;
    });

    return command;
}

static async Task<int> RunAsync(
    ProvisioningMode mode,
    string templatePath,
    string? basePath,
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
        var resolvedSchemaPath = ResolveSchemaPath(schemaPath);
        var resolvedBasePath = ResolveBasePath(basePath);

        var tokens = ProvisioningTokens.Create(projectCode, projectName, clientName, editors);
        var request = new ProvisioningRequest(
            Mode: mode,
            TemplatePath: templatePath,
            BasePath: resolvedBasePath,
            SchemaPath: resolvedSchemaPath,
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

static async Task<int> ValidateAsync(string templatePath, string? schemaPath)
{
    try
    {
        var resolvedSchemaPath = ResolveSchemaPath(schemaPath);
        var loader = new FolderTemplateLoader();
        var loaded = await loader.LoadAsync(templatePath, resolvedSchemaPath, CancellationToken.None);

        Console.WriteLine($"provisioner: validate ok template={loaded.Template.TemplateKey}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Provisioner validation failed: {ex.Message}");
        return 1;
    }
}

static string ResolveSchemaPath(string? schemaPath)
{
    if (!string.IsNullOrWhiteSpace(schemaPath))
    {
        if (Uri.TryCreate(schemaPath, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https"))
        {
            throw new InvalidOperationException("Remote schema URLs are not supported. Provide a local schema path.");
        }

        return Path.GetFullPath(schemaPath);
    }

    var baseDir = AppContext.BaseDirectory;
    var defaultSchema = Path.Combine(baseDir, "artifacts", "schemas", "mgf.folderTemplate.schema.json");
    if (!File.Exists(defaultSchema))
    {
        throw new InvalidOperationException($"Default schema not found at {defaultSchema}. Provide --schema explicitly.");
    }

    return defaultSchema;
}

static string ResolveBasePath(string? basePath)
{
    if (!string.IsNullOrWhiteSpace(basePath))
    {
        return Path.GetFullPath(basePath);
    }

    var repoRoot = FindRepoRoot();
    var defaultBase = Path.Combine(repoRoot, "runtime", "provisioner_runs");
    var fullDefault = Path.GetFullPath(defaultBase);

    if (!fullDefault.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Default base path must be within the repository.");
    }

    return fullDefault;
}

static string FindRepoRoot()
{
    var startPaths = new[]
    {
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory
    };

    foreach (var start in startPaths)
    {
        var current = new DirectoryInfo(start);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MGF.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }
    }

    throw new InvalidOperationException($"Could not locate repo root (MGF.sln) from {Directory.GetCurrentDirectory()}.");
}
