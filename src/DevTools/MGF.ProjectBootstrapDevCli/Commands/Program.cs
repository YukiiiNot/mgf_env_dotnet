using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MGF.Data.Configuration;
using MGF.Data.Stores.Operations;
using MGF.Operations.Runtime;
using MGF.UseCases.Operations.Projects.CreateTestProject;
using MGF.UseCases.Operations.Projects.GetProjectSnapshot;
using MGF.ProjectBootstrapCli;
using Npgsql;

var root = new RootCommand("MGF Project Bootstrap Dev Tool");
root.AddCommand(CreateSeedDeliverablesCommand());
root.AddCommand(CreateSeedE2EDeliveryEmailCommand());
root.AddCommand(CreateDevTestCleanCommand());
root.AddCommand(CreateTestProjectCommand());

var parser = new CommandLineBuilder(root).UseDefaults().Build();
return await parser.InvokeAsync(args);

static Command CreateSeedDeliverablesCommand()
{
    var command = new Command("seed-deliverables", "copy a local file into LucidLink Final_Masters for test deliveries.");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID to seed (e.g., prj_...).",
        IsRequired = true
    };

    var fileOption = new Option<string>("--file")
    {
        Description = "Local file path to seed into Final_Masters.",
        IsRequired = true
    };

    var targetOption = new Option<string>("--target")
    {
        Description = "Target folder (final-masters only for now).",
        Arity = ArgumentArity.ZeroOrOne
    };
    targetOption.SetDefaultValue("final-masters");

    var testModeOption = new Option<bool?>("--testMode")
    {
        Description = "Use test_run storage root (default: true).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var overwriteOption = new Option<bool?>("--overwrite")
    {
        Description = "Overwrite existing file in target (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    command.AddOption(projectIdOption);
    command.AddOption(fileOption);
    command.AddOption(targetOption);
    command.AddOption(testModeOption);
    command.AddOption(overwriteOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var filePath = context.ParseResult.GetValueForOption(fileOption) ?? string.Empty;
        var target = context.ParseResult.GetValueForOption(targetOption) ?? "final-masters";
        var testMode = context.ParseResult.GetValueForOption(testModeOption) ?? true;
        var overwrite = context.ParseResult.GetValueForOption(overwriteOption) ?? false;

        var exitCode = await SeedDeliverablesAsync(projectId, filePath, target, testMode, overwrite);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateSeedE2EDeliveryEmailCommand()
{
    var command = new Command("seed-e2e-delivery-email", "prepare a test project for delivery-email e2e by setting canonical recipients.");

    var emailOption = new Option<string>("--email")
    {
        Description = "Recipient email to set on the primary contact.",
        IsRequired = true
    };

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Use an existing project instead of creating/finding the test fixture.",
        IsRequired = false
    };

    var testKeyOption = new Option<string>("--testKey")
    {
        Description = "Metadata key to identify the test project (default: bootstrap_test).",
        IsRequired = false
    };
    testKeyOption.SetDefaultValue("bootstrap_test");

    var forceNewOption = new Option<bool>("--forceNew")
    {
        Description = "Create a new test project even if one already exists.",
        IsRequired = false
    };

    command.AddOption(emailOption);
    command.AddOption(projectIdOption);
    command.AddOption(testKeyOption);
    command.AddOption(forceNewOption);

    command.SetHandler(async context =>
    {
        var exitCode = await SeedE2EDeliveryEmailAsync(
            email: context.ParseResult.GetValueForOption(emailOption) ?? string.Empty,
            projectId: context.ParseResult.GetValueForOption(projectIdOption),
            testKey: context.ParseResult.GetValueForOption(testKeyOption) ?? "bootstrap_test",
            forceNew: context.ParseResult.GetValueForOption(forceNewOption));
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateDevTestCleanCommand()
{
    var command = new Command("devtest-clean", "clean DevTest roots using the root contract (report-only unless --dryRun false).");

    var providerOption = new Option<string>("--provider")
    {
        Description = "Storage provider key (dropbox|lucidlink|nas).",
        IsRequired = true
    };

    var dryRunOption = new Option<bool?>("--dryRun")
    {
        Description = "Report only (default: true). Use --dryRun false to apply.",
        Arity = ArgumentArity.ZeroOrOne
    };
    dryRunOption.SetDefaultValue(true);

    var maxItemsOption = new Option<int?>("--maxItems")
    {
        Description = "Max items allowed to quarantine (default: 250).",
        Arity = ArgumentArity.ZeroOrOne
    };
    maxItemsOption.SetDefaultValue(250);

    var maxBytesOption = new Option<long?>("--maxBytes")
    {
        Description = "Max bytes allowed to quarantine (default: 2GB).",
        Arity = ArgumentArity.ZeroOrOne
    };
    maxBytesOption.SetDefaultValue(2L * 1024 * 1024 * 1024);

    var forceUnknownOption = new Option<bool?>("--forceUnknownSize")
    {
        Description = "Allow moving unknown-size entries (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };
    forceUnknownOption.SetDefaultValue(false);

    command.AddOption(providerOption);
    command.AddOption(dryRunOption);
    command.AddOption(maxItemsOption);
    command.AddOption(maxBytesOption);
    command.AddOption(forceUnknownOption);

    command.SetHandler(async context =>
    {
        var provider = context.ParseResult.GetValueForOption(providerOption) ?? string.Empty;
        var dryRun = context.ParseResult.GetValueForOption(dryRunOption) ?? true;
        var maxItems = context.ParseResult.GetValueForOption(maxItemsOption) ?? 250;
        var maxBytes = context.ParseResult.GetValueForOption(maxBytesOption) ?? (2L * 1024 * 1024 * 1024);
        var forceUnknown = context.ParseResult.GetValueForOption(forceUnknownOption) ?? false;

        var exitCode = await RunDevTestCleanAsync(provider, dryRun, maxItems, maxBytes, forceUnknown);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateTestProjectCommand()
{
    var command = new Command("create-test", "create a test client/editor/project for repeatable provisioning.");

    var testKeyOption = new Option<string>("--testKey")
    {
        Description = "Metadata key to identify the test project (default: bootstrap_test).",
        IsRequired = false
    };
    testKeyOption.SetDefaultValue("bootstrap_test");

    var clientNameOption = new Option<string>("--clientName")
    {
        Description = "Client display name for the test project.",
        IsRequired = false
    };
    clientNameOption.SetDefaultValue("MGF Test Client");

    var projectNameOption = new Option<string>("--projectName")
    {
        Description = "Project name for the test project.",
        IsRequired = false
    };
    projectNameOption.SetDefaultValue("MGF Bootstrap Test");

    var editorFirstOption = new Option<string>("--editorFirstName")
    {
        Description = "Editor first name.",
        IsRequired = false
    };
    editorFirstOption.SetDefaultValue("Test");

    var editorLastOption = new Option<string>("--editorLastName")
    {
        Description = "Editor last name.",
        IsRequired = false
    };
    editorLastOption.SetDefaultValue("Editor");

    var editorInitialsOption = new Option<string>("--editorInitials")
    {
        Description = "Editor initials.",
        IsRequired = false
    };
    editorInitialsOption.SetDefaultValue("TE");

    var forceNewOption = new Option<bool>("--forceNew")
    {
        Description = "Create a new test project even if one already exists.",
        IsRequired = false
    };

    command.AddOption(testKeyOption);
    command.AddOption(clientNameOption);
    command.AddOption(projectNameOption);
    command.AddOption(editorFirstOption);
    command.AddOption(editorLastOption);
    command.AddOption(editorInitialsOption);
    command.AddOption(forceNewOption);

    command.SetHandler(async context =>
    {
        var exitCode = await CreateTestProjectAsync(
            testKey: context.ParseResult.GetValueForOption(testKeyOption) ?? "bootstrap_test",
            clientName: context.ParseResult.GetValueForOption(clientNameOption) ?? "MGF Test Client",
            projectName: context.ParseResult.GetValueForOption(projectNameOption) ?? "MGF Bootstrap Test",
            editorFirstName: context.ParseResult.GetValueForOption(editorFirstOption) ?? "Test",
            editorLastName: context.ParseResult.GetValueForOption(editorLastOption) ?? "Editor",
            editorInitials: context.ParseResult.GetValueForOption(editorInitialsOption) ?? "TE",
            forceNew: context.ParseResult.GetValueForOption(forceNewOption)
        );
        context.ExitCode = exitCode;
    });

    return command;
}

static async Task<int> SeedDeliverablesAsync(
    string projectId,
    string filePath,
    string target,
    bool testMode,
    bool overwrite)
{
    if (string.IsNullOrWhiteSpace(projectId))
    {
        Console.Error.WriteLine("bootstrap: seed-deliverables missing projectId.");
        return 1;
    }

    if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
    {
        Console.Error.WriteLine($"bootstrap: seed-deliverables file not found: {filePath}");
        return 1;
    }

    if (!string.Equals(target, "final-masters", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"bootstrap: seed-deliverables unsupported target: {target}");
        return 1;
    }

    var config = BuildConfiguration();
    var connectionString = DatabaseConnection.ResolveConnectionString(config);

    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    var rootPath = config["Storage:LucidLinkRoot"];
    if (string.IsNullOrWhiteSpace(rootPath))
    {
        Console.Error.WriteLine("bootstrap: seed-deliverables failed: Storage:LucidLinkRoot not configured.");
        return 1;
    }

    var rootKey = testMode ? "test_run" : "project_container";
    string? folderRelpath = null;

    await using (var cmd = new NpgsqlCommand(
                     """
                     SELECT folder_relpath
                     FROM public.project_storage_roots
                     WHERE project_id = @project_id
                       AND storage_provider_key = 'lucidlink'
                       AND root_key = @root_key
                     ORDER BY created_at DESC
                     LIMIT 1;
                     """,
                     conn
                 ))
    {
        cmd.Parameters.AddWithValue("project_id", projectId);
        cmd.Parameters.AddWithValue("root_key", rootKey);

        var result = await cmd.ExecuteScalarAsync();
        folderRelpath = result?.ToString();
    }

    if (string.IsNullOrWhiteSpace(folderRelpath))
    {
        Console.Error.WriteLine($"bootstrap: seed-deliverables no lucidlink storage root found (root_key={rootKey}).");
        return 1;
    }

    var rootFull = Path.GetFullPath(rootPath);
    var containerRoot = Path.GetFullPath(Path.Combine(rootFull, folderRelpath));
    if (!containerRoot.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(containerRoot, rootFull, StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("bootstrap: seed-deliverables failed: resolved path outside LucidLink root.");
        return 1;
    }

    var finalMasters = Path.Combine(containerRoot, "02_Renders", "Final_Masters");
    Directory.CreateDirectory(finalMasters);

    var fileName = Path.GetFileName(filePath);
    var destPath = Path.Combine(finalMasters, fileName);
    if (File.Exists(destPath) && !overwrite)
    {
        Console.Error.WriteLine($"bootstrap: seed-deliverables target exists: {destPath}");
        return 1;
    }

    File.Copy(filePath, destPath, overwrite);
    Console.WriteLine($"bootstrap: seeded deliverable to {destPath}");
    return 0;
}

static async Task<int> SeedE2EDeliveryEmailAsync(
    string email,
    string? projectId,
    string testKey,
    bool forceNew)
{
    if (string.IsNullOrWhiteSpace(email))
    {
        Console.Error.WriteLine("bootstrap: seed-e2e-delivery-email missing email.");
        return 1;
    }

    try
    {
        var config = BuildConfiguration();
        var runtime = OperationsRuntime.Create(config);

        var resolvedProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim();
        string? projectCode = null;
        string? projectName = null;
        string? clientId = null;

        if (string.IsNullOrWhiteSpace(resolvedProjectId))
        {
            var createResult = await runtime.CreateTestProject.ExecuteAsync(new CreateTestProjectRequest(
                TestKey: testKey,
                ClientName: "MGF Test Client",
                ProjectName: "MGF Bootstrap Test",
                EditorFirstName: "Test",
                EditorLastName: "Editor",
                EditorInitials: "TE",
                ForceNew: forceNew));

            resolvedProjectId = createResult.Created
                ? createResult.CreatedProject?.ProjectId
                : createResult.ExistingProject?.ProjectId;
            projectCode = createResult.Created
                ? createResult.CreatedProject?.ProjectCode
                : createResult.ExistingProject?.ProjectCode;
            projectName = createResult.Created
                ? createResult.CreatedProject?.ProjectName
                : createResult.ExistingProject?.ProjectName;
            clientId = createResult.Created
                ? createResult.CreatedProject?.ClientId
                : createResult.ExistingProject?.ClientId;
        }

        if (string.IsNullOrWhiteSpace(resolvedProjectId))
        {
            Console.Error.WriteLine("bootstrap: seed-e2e-delivery-email failed: missing project.");
            return 1;
        }

        var snapshot = await runtime.GetProjectSnapshot.ExecuteAsync(
            new GetProjectSnapshotRequest(resolvedProjectId, IncludeStorageRoots: false));

        if (snapshot is null)
        {
            Console.Error.WriteLine($"bootstrap: seed-e2e-delivery-email failed: project not found ({resolvedProjectId}).");
            return 1;
        }

        clientId ??= snapshot.Project.ClientId;
        projectCode ??= snapshot.Project.ProjectCode;
        projectName ??= snapshot.Project.ProjectName;

        var contactStore = new ProjectContactOpsStore(config);
        var contactResult = await contactStore.EnsurePrimaryContactEmailAsync(clientId, email);
        if (contactResult is null)
        {
            Console.Error.WriteLine($"bootstrap: seed-e2e-delivery-email failed: client has no primary contact (client_id={clientId}).");
            return 1;
        }

        var stableShareUrl = TryReadStableShareUrl(snapshot.Project.MetadataJson);
        var stableShareUrlExists = !string.IsNullOrWhiteSpace(stableShareUrl);

        Console.WriteLine($"bootstrap: seed-e2e-delivery-email project_id={resolvedProjectId}");
        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            Console.WriteLine($"bootstrap: seed-e2e-delivery-email project_code={projectCode}");
        }
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            Console.WriteLine($"bootstrap: seed-e2e-delivery-email project_name={projectName}");
        }
        Console.WriteLine($"bootstrap: seed-e2e-delivery-email recipients={string.Join(',', new[] { contactResult.Email })}");
        Console.WriteLine($"bootstrap: seed-e2e-delivery-email stable_share_url_exists={stableShareUrlExists}");

        if (!stableShareUrlExists)
        {
            var deliveryCommand = $"dotnet run -c Release --project src\\Operations\\MGF.ProjectBootstrapCli -- deliver --projectId {resolvedProjectId} --editorInitials TE --testMode true";
            Console.WriteLine($"bootstrap: seed-e2e-delivery-email run: {deliveryCommand}");
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: seed-e2e-delivery-email failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> RunDevTestCleanAsync(
    string providerKey,
    bool dryRun,
    int maxItems,
    long maxBytes,
    bool forceUnknownSize)
{
    try
    {
        var config = BuildConfiguration();
        var rootPath = ResolveRootPath(providerKey, config);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            Console.Error.WriteLine($"devtest-clean: root path not configured for provider={providerKey}");
            return 1;
        }

        if (!IsDevTestRoot(rootPath))
        {
            Console.Error.WriteLine($"devtest-clean: refusing to operate outside DevTest root: {rootPath}");
            return 1;
        }

        rootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(rootPath))
        {
            Console.Error.WriteLine($"devtest-clean: root path does not exist: {rootPath}");
            return 1;
        }

        var runtime = OperationsRuntime.Create(config);
        var contract = await runtime.StorageRootContracts.GetActiveContractAsync(providerKey, rootKey: "root");
        if (contract is null)
        {
            Console.Error.WriteLine($"devtest-clean: no storage_root_contracts row for provider={providerKey} root_key=root");
            return 1;
        }

        var effectiveContract = ApplyDevTestOverrides(providerKey, new DevTestRootContract(
            RequiredFolders: contract.RequiredFolders,
            OptionalFolders: contract.OptionalFolders,
            AllowedExtras: contract.AllowedExtras,
            AllowedRootFiles: contract.AllowedRootFiles,
            QuarantineRelpath: contract.QuarantineRelpath));

        var options = new DevTestRootOptions(
            DryRun: dryRun,
            MaxItems: maxItems,
            MaxBytes: maxBytes,
            ForceUnknownSize: forceUnknownSize,
            AllowMeasure: true,
            TimestampUtc: DateTimeOffset.UtcNow
        );

        var plan = DevTestRootCleaner.Plan(rootPath, effectiveContract, options);

        Console.WriteLine($"devtest-clean: provider={providerKey} root={rootPath}");
        Console.WriteLine($"devtest-clean: dryRun={dryRun} maxItems={maxItems} maxBytes={maxBytes} forceUnknownSize={forceUnknownSize}");
        Console.WriteLine($"devtest-clean: missing_required={plan.MissingRequired.Count} missing_optional={plan.MissingOptional.Count} unknown={plan.UnknownEntries.Count} root_files={plan.RootFiles.Count} legacy_manifests={plan.LegacyManifestFiles.Count} guardrail_blocks={plan.GuardrailBlocks.Count}");

        if (plan.MissingRequired.Count > 0)
        {
            Console.WriteLine("devtest-clean: missing_required:");
            foreach (var missing in plan.MissingRequired)
            {
                Console.WriteLine($"  - {missing}");
            }
        }

        if (plan.UnknownMovePlans.Count > 0)
        {
            Console.WriteLine("devtest-clean: quarantine_candidates:");
            foreach (var move in plan.UnknownMovePlans)
            {
                Console.WriteLine($"  - {move.Name} ({move.Kind})");
            }
        }

        if (plan.LegacyManifestFiles.Count > 0)
        {
            Console.WriteLine("devtest-clean: legacy_manifest_files:");
            foreach (var legacy in plan.LegacyManifestFiles)
            {
                Console.WriteLine($"  - {legacy.SourcePath}");
            }
        }

        if (dryRun)
        {
            return 0;
        }

        var applyResult = DevTestRootCleaner.Apply(plan, effectiveContract, options);

        Console.WriteLine($"devtest-clean: actions={applyResult.Actions.Count} errors={applyResult.Errors.Count}");
        foreach (var action in applyResult.Actions)
        {
            Console.WriteLine($"  - {action}");
        }

        foreach (var error in applyResult.Errors)
        {
            Console.WriteLine($"  - error:{error}");
        }

        return applyResult.Errors.Count == 0 ? 0 : 2;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"devtest-clean: failed: {ex.Message}");
        return 1;
    }
}

static string? TryReadStableShareUrl(string metadataJson)
{
    if (string.IsNullOrWhiteSpace(metadataJson))
    {
        return null;
    }

    try
    {
        using var doc = JsonDocument.Parse(metadataJson);
        if (!doc.RootElement.TryGetProperty("delivery", out var delivery))
        {
            return null;
        }

        if (!delivery.TryGetProperty("current", out var current))
        {
            return null;
        }

        if (current.TryGetProperty("stableShareUrl", out var stableShareUrl)
            && stableShareUrl.ValueKind == JsonValueKind.String)
        {
            return stableShareUrl.GetString();
        }

        if (current.TryGetProperty("shareUrl", out var shareUrl)
            && shareUrl.ValueKind == JsonValueKind.String)
        {
            return shareUrl.GetString();
        }

        return null;
    }
    catch
    {
        return null;
    }
}

static string? ResolveRootPath(string providerKey, IConfiguration config)
{
    return providerKey.ToLowerInvariant() switch
    {
        "dropbox" => config["Storage:DropboxRoot"],
        "lucidlink" => config["Storage:LucidLinkRoot"],
        "nas" => config["Storage:NasRoot"],
        _ => null
    };
}

static bool IsDevTestRoot(string rootPath)
{
    return rootPath.IndexOf("06_DevTest", StringComparison.OrdinalIgnoreCase) >= 0;
}

static DevTestRootContract ApplyDevTestOverrides(string providerKey, DevTestRootContract contract)
{
    var required = new HashSet<string>(contract.RequiredFolders, StringComparer.OrdinalIgnoreCase)
    {
        "00_Admin",
        "99_Dump"
    };

    if (providerKey.Equals("dropbox", StringComparison.OrdinalIgnoreCase)
        || providerKey.Equals("lucidlink", StringComparison.OrdinalIgnoreCase)
        || providerKey.Equals("nas", StringComparison.OrdinalIgnoreCase))
    {
        required.Add("99_TestRuns");
    }

    var optional = new HashSet<string>(contract.OptionalFolders, StringComparer.OrdinalIgnoreCase)
    {
        "99_Legacy",
        "04_Staging"
    };

    return contract with
    {
        RequiredFolders = required.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
        OptionalFolders = optional.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
    };
}

static async Task<int> CreateTestProjectAsync(
    string testKey,
    string clientName,
    string projectName,
    string editorFirstName,
    string editorLastName,
    string editorInitials,
    bool forceNew
)
{
    try
    {
        var config = BuildConfiguration();
        var runtime = OperationsRuntime.Create(config);
        var result = await runtime.CreateTestProject.ExecuteAsync(new CreateTestProjectRequest(
            testKey,
            clientName,
            projectName,
            editorFirstName,
            editorLastName,
            editorInitials,
            forceNew));

        if (!result.Created && result.ExistingProject is not null)
        {
            Console.WriteLine("bootstrap: existing test project found (use --forceNew to create another)");
            Console.WriteLine($"project_id={result.ExistingProject.ProjectId}");
            Console.WriteLine($"project_code={result.ExistingProject.ProjectCode}");
            Console.WriteLine($"name={result.ExistingProject.ProjectName}");
            Console.WriteLine($"client_id={result.ExistingProject.ClientId}");
            return 0;
        }

        if (result.CreatedProject is null)
        {
            Console.Error.WriteLine("bootstrap: create-test failed: missing created project.");
            return 1;
        }

        Console.WriteLine("bootstrap: created test project");
        Console.WriteLine($"project_id={result.CreatedProject.ProjectId}");
        Console.WriteLine($"project_code={result.CreatedProject.ProjectCode}");
        Console.WriteLine($"name={result.CreatedProject.ProjectName}");
        Console.WriteLine($"client_id={result.CreatedProject.ClientId}");
        Console.WriteLine($"person_id={result.CreatedProject.PersonId}");
        Console.WriteLine($"editor_initials={result.CreatedProject.EditorInitials}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: create-test failed: {ex.Message}");
        return 1;
    }
}

static IConfiguration BuildConfiguration()
{
    return OperationsRuntimeConfiguration.BuildConfiguration();
}
