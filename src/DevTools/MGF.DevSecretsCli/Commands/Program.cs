using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using MGF.DevSecretsCli;

var root = new RootCommand("MGF developer secrets export/import tool (appsettings.Development.json)");
root.AddCommand(CreateExportCommand());
root.AddCommand(CreateImportCommand());
root.AddCommand(CreateValidateCommand());

var parser = new CommandLineBuilder(root).UseDefaults().Build();
return await parser.InvokeAsync(args);

static Command CreateExportCommand()
{
    var command = new Command("export", "export allowed keys from config/appsettings.Development.json.");

    var outOption = new Option<string?>("--out")
    {
        Description = "Output path for dev-secrets.export.json (default: repo root ./dev-secrets.export.json).",
        IsRequired = false
    };

    var requiredOption = new Option<string?>("--required")
    {
        Description = "Optional path to secrets.required.json (default: tools/dev-secrets/secrets.required.json).",
        IsRequired = false
    };

    var verboseOption = new Option<bool>("--verbose")
    {
        Description = "Print key names (never values).",
        IsRequired = false
    };

    command.AddOption(outOption);
    command.AddOption(requiredOption);
    command.AddOption(verboseOption);

    command.SetHandler(async context =>
    {
        var outPath = context.ParseResult.GetValueForOption(outOption);
        var requiredPath = context.ParseResult.GetValueForOption(requiredOption);
        var verbose = context.ParseResult.GetValueForOption(verboseOption);

        var exitCode = await DevSecretsCommands.ExportAsync(outPath, requiredPath, verbose, CancellationToken.None);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateImportCommand()
{
    var command = new Command("import", "import dev-secrets.export.json into config/appsettings.Development.json.");

    var fileOption = new Option<string>(new[] { "--file", "--in" })
    {
        Description = "Path to dev-secrets.export.json.",
        IsRequired = true
    };

    var requiredOption = new Option<string?>("--required")
    {
        Description = "Optional path to secrets.required.json (default: tools/dev-secrets/secrets.required.json).",
        IsRequired = false
    };

    var dryRunOption = new Option<bool>("--dry-run")
    {
        Description = "Print what would be set without writing.",
        IsRequired = false
    };

    var verboseOption = new Option<bool>("--verbose")
    {
        Description = "Print key names (never values).",
        IsRequired = false
    };

    var forceOption = new Option<bool>("--force")
    {
        Description = "Overwrite existing values in appsettings.Development.json.",
        IsRequired = false
    };

    command.AddOption(fileOption);
    command.AddOption(requiredOption);
    command.AddOption(dryRunOption);
    command.AddOption(verboseOption);
    command.AddOption(forceOption);

    command.SetHandler(async context =>
    {
        var inPath = context.ParseResult.GetValueForOption(fileOption) ?? string.Empty;
        var requiredPath = context.ParseResult.GetValueForOption(requiredOption);
        var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
        var verbose = context.ParseResult.GetValueForOption(verboseOption);
        var force = context.ParseResult.GetValueForOption(forceOption);

        var exitCode = await DevSecretsCommands.ImportAsync(inPath, requiredPath, dryRun, verbose, force, CancellationToken.None);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateValidateCommand()
{
    var command = new Command("validate", "validate required keys in config/appsettings.Development.json.");

    var requiredOption = new Option<string?>("--required")
    {
        Description = "Optional path to secrets.required.json (default: tools/dev-secrets/secrets.required.json).",
        IsRequired = false
    };

    command.AddOption(requiredOption);

    command.SetHandler(async context =>
    {
        var requiredPath = context.ParseResult.GetValueForOption(requiredOption);
        var exitCode = await DevSecretsCommands.ValidateAsync(requiredPath, CancellationToken.None);
        context.ExitCode = exitCode;
    });

    return command;
}

