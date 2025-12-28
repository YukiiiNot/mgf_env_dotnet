using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MGF.Domain.Entities;
using MGF.Infrastructure.Configuration;
using MGF.Infrastructure.Data;
using Npgsql;

var root = new RootCommand("MGF Project Bootstrap Job Tool");
root.AddCommand(CreateEnqueueCommand());
root.AddCommand(CreateReadyCommand());
root.AddCommand(CreateToArchiveCommand());
root.AddCommand(CreateArchiveCommand());
root.AddCommand(CreateRootAuditCommand());
root.AddCommand(CreateRootRepairCommand());
root.AddCommand(CreateRootShowCommand());
root.AddCommand(CreateShowCommand());
root.AddCommand(CreateListCommand());
root.AddCommand(CreateTestProjectCommand());
root.AddCommand(CreateResetJobsCommand());
root.AddCommand(CreateJobsRequeueStaleCommand());

var parser = new CommandLineBuilder(root).UseDefaults().Build();
return await parser.InvokeAsync(args);

static Command CreateEnqueueCommand()
{
    var command = new Command("enqueue", "enqueue a project.bootstrap job.");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID to bootstrap (e.g., prj_...).",
        IsRequired = true
    };

    var verifyOption = new Option<bool?>("--verifyDomainRoots")
    {
        Description = "Whether to verify domain roots (default: true).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var createOption = new Option<bool?>("--createDomainRoots")
    {
        Description = "Whether to create missing domain roots (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var provisionOption = new Option<bool?>("--provisionProjectContainers")
    {
        Description = "Whether to create project containers (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var repairOption = new Option<bool?>("--allowRepair")
    {
        Description = "Whether to attempt repair after failed verify (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var editorsOption = new Option<string[]>("--editors")
    {
        Description = "Comma-separated editor initials (e.g., ER,AB) or repeatable values.",
        Arity = ArgumentArity.ZeroOrMore
    };
    editorsOption.AllowMultipleArgumentsPerToken = true;

    var sandboxOption = new Option<bool?>("--forceSandbox")
    {
        Description = "Use repo runtime sandbox roots (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var allowNonRealOption = new Option<bool?>("--allowNonReal")
    {
        Description = "Allow provisioning non-real data_profile projects (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var forceOption = new Option<bool?>("--force")
    {
        Description = "Force provisioning even if project status is not ready_to_provision (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var testModeOption = new Option<bool?>("--testMode")
    {
        Description = "Provision containers under 99_TestRuns per domain (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var allowTestCleanupOption = new Option<bool?>("--allowTestCleanup")
    {
        Description = "Allow cleanup of existing test target folder when testMode=true (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    command.AddOption(projectIdOption);
    command.AddOption(verifyOption);
    command.AddOption(createOption);
    command.AddOption(provisionOption);
    command.AddOption(repairOption);
    command.AddOption(editorsOption);
    command.AddOption(sandboxOption);
    command.AddOption(allowNonRealOption);
    command.AddOption(forceOption);
    command.AddOption(testModeOption);
    command.AddOption(allowTestCleanupOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var payload = new ProjectBootstrapJobPayload(
            ProjectId: projectId,
            EditorInitials: ParseEditors(context.ParseResult.GetValueForOption(editorsOption) ?? Array.Empty<string>()),
            VerifyDomainRoots: context.ParseResult.GetValueForOption(verifyOption) ?? true,
            CreateDomainRoots: context.ParseResult.GetValueForOption(createOption) ?? false,
            ProvisionProjectContainers: context.ParseResult.GetValueForOption(provisionOption) ?? false,
            AllowRepair: context.ParseResult.GetValueForOption(repairOption) ?? false,
            ForceSandbox: context.ParseResult.GetValueForOption(sandboxOption) ?? false,
            AllowNonReal: context.ParseResult.GetValueForOption(allowNonRealOption) ?? false,
            Force: context.ParseResult.GetValueForOption(forceOption) ?? false,
            TestMode: context.ParseResult.GetValueForOption(testModeOption) ?? false,
            AllowTestCleanup: context.ParseResult.GetValueForOption(allowTestCleanupOption) ?? false
        );

        var exitCode = await EnqueueAsync(payload);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateReadyCommand()
{
    var command = new Command("ready", "mark a project as ready_to_provision.");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID to mark ready.",
        IsRequired = true
    };

    command.AddOption(projectIdOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var exitCode = await MarkReadyAsync(projectId);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateToArchiveCommand()
{
    var command = new Command("to-archive", "mark a project as to_archive.");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID to mark to_archive.",
        IsRequired = true
    };

    command.AddOption(projectIdOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var exitCode = await MarkToArchiveAsync(projectId);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateArchiveCommand()
{
    var command = new Command("archive", "enqueue a project.archive job.");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID to archive (e.g., prj_...).",
        IsRequired = true
    };

    var editorsOption = new Option<string[]>("--editors")
    {
        Description = "Comma-separated editor initials (e.g., ER,AB) or repeatable values.",
        Arity = ArgumentArity.ZeroOrMore
    };
    editorsOption.AllowMultipleArgumentsPerToken = true;

    var testModeOption = new Option<bool?>("--testMode")
    {
        Description = "Use 99_TestRuns per domain (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var allowTestCleanupOption = new Option<bool?>("--allowTestCleanup")
    {
        Description = "Allow cleanup of existing test target folder when testMode=true (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var allowNonRealOption = new Option<bool?>("--allowNonReal")
    {
        Description = "Allow archiving non-real data_profile projects (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var forceOption = new Option<bool?>("--force")
    {
        Description = "Force archive even if project status is not to_archive (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    command.AddOption(projectIdOption);
    command.AddOption(editorsOption);
    command.AddOption(testModeOption);
    command.AddOption(allowTestCleanupOption);
    command.AddOption(allowNonRealOption);
    command.AddOption(forceOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var payload = new ProjectArchiveJobPayload(
            ProjectId: projectId,
            EditorInitials: ParseEditors(context.ParseResult.GetValueForOption(editorsOption) ?? Array.Empty<string>()),
            TestMode: context.ParseResult.GetValueForOption(testModeOption) ?? false,
            AllowTestCleanup: context.ParseResult.GetValueForOption(allowTestCleanupOption) ?? false,
            AllowNonReal: context.ParseResult.GetValueForOption(allowNonRealOption) ?? false,
            Force: context.ParseResult.GetValueForOption(forceOption) ?? false
        );

        var exitCode = await EnqueueArchiveAsync(payload);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateRootAuditCommand()
{
    var command = new Command("root-audit", "enqueue a domain.root_integrity job (report-only).");

    var providerOption = new Option<string>("--provider")
    {
        Description = "Storage provider key (dropbox|lucidlink|nas).",
        IsRequired = true
    };

    var rootKeyOption = new Option<string>("--rootKey")
    {
        Description = "Root key (default: root).",
        IsRequired = false
    };
    rootKeyOption.SetDefaultValue("root");

    var dryRunOption = new Option<bool>("--dryRun")
    {
        Description = "Report only (default: true).",
        IsRequired = false
    };
    dryRunOption.SetDefaultValue(true);

    var maxItemsOption = new Option<int?>("--maxItems")
    {
        Description = "Max items allowed for quarantine move (optional).",
        IsRequired = false
    };

    var maxBytesOption = new Option<long?>("--maxBytes")
    {
        Description = "Max bytes allowed for quarantine move (optional).",
        IsRequired = false
    };

    var quarantineOption = new Option<string?>("--quarantineRelpath")
    {
        Description = "Override quarantine relpath (optional).",
        IsRequired = false
    };

    command.AddOption(providerOption);
    command.AddOption(rootKeyOption);
    command.AddOption(dryRunOption);
    command.AddOption(maxItemsOption);
    command.AddOption(maxBytesOption);
    command.AddOption(quarantineOption);

    command.SetHandler(async context =>
    {
        var payload = new RootIntegrityJobPayload(
            ProviderKey: context.ParseResult.GetValueForOption(providerOption) ?? string.Empty,
            RootKey: context.ParseResult.GetValueForOption(rootKeyOption) ?? "root",
            Mode: "report",
            DryRun: context.ParseResult.GetValueForOption(dryRunOption),
            QuarantineRelpath: context.ParseResult.GetValueForOption(quarantineOption),
            MaxItems: context.ParseResult.GetValueForOption(maxItemsOption),
            MaxBytes: context.ParseResult.GetValueForOption(maxBytesOption)
        );

        var exitCode = await EnqueueRootIntegrityAsync(payload);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateRootRepairCommand()
{
    var command = new Command("root-repair", "enqueue a domain.root_integrity job in repair mode (explicit dryRun=false required).");

    var providerOption = new Option<string>("--provider")
    {
        Description = "Storage provider key (dropbox|lucidlink|nas).",
        IsRequired = true
    };

    var rootKeyOption = new Option<string>("--rootKey")
    {
        Description = "Root key (default: root).",
        IsRequired = false
    };
    rootKeyOption.SetDefaultValue("root");

    var dryRunOption = new Option<bool>("--dryRun")
    {
        Description = "If true, report only. To execute repair, set --dryRun false.",
        IsRequired = false
    };
    dryRunOption.SetDefaultValue(true);

    var maxItemsOption = new Option<int?>("--maxItems")
    {
        Description = "Max items allowed for quarantine move (optional).",
        IsRequired = false
    };

    var maxBytesOption = new Option<long?>("--maxBytes")
    {
        Description = "Max bytes allowed for quarantine move (optional).",
        IsRequired = false
    };

    var quarantineOption = new Option<string?>("--quarantineRelpath")
    {
        Description = "Override quarantine relpath (optional).",
        IsRequired = false
    };

    command.AddOption(providerOption);
    command.AddOption(rootKeyOption);
    command.AddOption(dryRunOption);
    command.AddOption(maxItemsOption);
    command.AddOption(maxBytesOption);
    command.AddOption(quarantineOption);

    command.SetHandler(async context =>
    {
        var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
        if (dryRun)
        {
            Console.Error.WriteLine("root-repair: refusing to enqueue repair without --dryRun false.");
            context.ExitCode = 1;
            return;
        }

        var payload = new RootIntegrityJobPayload(
            ProviderKey: context.ParseResult.GetValueForOption(providerOption) ?? string.Empty,
            RootKey: context.ParseResult.GetValueForOption(rootKeyOption) ?? "root",
            Mode: "repair",
            DryRun: dryRun,
            QuarantineRelpath: context.ParseResult.GetValueForOption(quarantineOption),
            MaxItems: context.ParseResult.GetValueForOption(maxItemsOption),
            MaxBytes: context.ParseResult.GetValueForOption(maxBytesOption)
        );

        var exitCode = await EnqueueRootIntegrityAsync(payload);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateRootShowCommand()
{
    var command = new Command("root-show", "show latest root integrity jobs for a provider/root.");

    var providerOption = new Option<string>("--provider")
    {
        Description = "Storage provider key (dropbox|lucidlink|nas).",
        IsRequired = true
    };

    var rootKeyOption = new Option<string>("--rootKey")
    {
        Description = "Root key (default: root).",
        IsRequired = false
    };
    rootKeyOption.SetDefaultValue("root");

    command.AddOption(providerOption);
    command.AddOption(rootKeyOption);

    command.SetHandler(async context =>
    {
        var provider = context.ParseResult.GetValueForOption(providerOption) ?? string.Empty;
        var rootKey = context.ParseResult.GetValueForOption(rootKeyOption) ?? "root";
        var exitCode = await ShowRootIntegrityAsync(provider, rootKey);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateShowCommand()
{
    var command = new Command("show", "show project metadata for a project id.");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID to inspect.",
        IsRequired = true
    };

    command.AddOption(projectIdOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var exitCode = await ShowProjectAsync(projectId);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateListCommand()
{
    var command = new Command("list", "list recent projects (id, code, name).");

    var limitOption = new Option<int>("--limit")
    {
        Description = "Max projects to list.",
        IsRequired = false
    };
    limitOption.SetDefaultValue(10);

    command.AddOption(limitOption);

    command.SetHandler(async context =>
    {
        var limit = context.ParseResult.GetValueForOption(limitOption);
        var exitCode = await ListProjectsAsync(limit);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateResetJobsCommand()
{
    var command = new Command("reset-jobs", "reset queued/running jobs for a project.");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID whose jobs should be reset.",
        IsRequired = true
    };

    var jobTypeOption = new Option<string>("--jobType")
    {
        Description = "Job type to reset (default: project.bootstrap).",
        IsRequired = false
    };
    jobTypeOption.SetDefaultValue("project.bootstrap");

    command.AddOption(projectIdOption);
    command.AddOption(jobTypeOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var jobTypeKey = context.ParseResult.GetValueForOption(jobTypeOption) ?? "project.bootstrap";
        var exitCode = await ResetJobsAsync(projectId, jobTypeKey);
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

static Command CreateJobsRequeueStaleCommand()
{
    var command = new Command("jobs-requeue-stale", "requeue stale running jobs with expired locks.");

    var dryRunOption = new Option<bool>("--dryRun")
    {
        Description = "If true, only report how many rows would be reset.",
        IsRequired = false
    };
    dryRunOption.SetDefaultValue(true);

    command.AddOption(dryRunOption);

    command.SetHandler(async context =>
    {
        var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
        var exitCode = await RequeueStaleJobsAsync(dryRun);
        context.ExitCode = exitCode;
    });

    return command;
}
static async Task<int> EnqueueAsync(ProjectBootstrapJobPayload payload)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await EnsureJobTypeExistsAsync(conn);

        var existing = await FindExistingJobAsync(conn, payload.ProjectId);
        if (!BootstrapJobGuard.ShouldEnqueue(existing, out var reason))
        {
            Console.WriteLine($"bootstrap: {reason} for project_id={payload.ProjectId}");
            return 0;
        }

        var jobId = EntityIds.NewWithPrefix("job");
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO public.jobs (job_id, job_type_key, payload, status_key, run_after, entity_type_key, entity_key)
            VALUES (@job_id, @job_type_key, @payload::jsonb, 'queued', now(), @entity_type_key, @entity_key);
            """,
            conn
        );

        cmd.Parameters.AddWithValue("job_id", jobId);
        cmd.Parameters.AddWithValue("job_type_key", "project.bootstrap");
        cmd.Parameters.AddWithValue("payload", payloadJson);
        cmd.Parameters.AddWithValue("entity_type_key", "project");
        cmd.Parameters.AddWithValue("entity_key", payload.ProjectId);

        await cmd.ExecuteNonQueryAsync();

        Console.WriteLine($"bootstrap: enqueued job_id={jobId} project_id={payload.ProjectId}");
        Console.WriteLine($"bootstrap: payload={payloadJson}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: enqueue failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> EnqueueArchiveAsync(ProjectArchiveJobPayload payload)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await EnsureArchiveJobTypeExistsAsync(conn);

        var existing = await FindExistingArchiveJobAsync(conn, payload.ProjectId);
        if (!ArchiveJobGuard.ShouldEnqueue(existing, out var reason))
        {
            Console.WriteLine($"archive: {reason} for project_id={payload.ProjectId}");
            return 0;
        }

        var jobId = EntityIds.NewWithPrefix("job");
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO public.jobs (job_id, job_type_key, payload, status_key, run_after, entity_type_key, entity_key)
            VALUES (@job_id, @job_type_key, @payload::jsonb, 'queued', now(), @entity_type_key, @entity_key);
            """,
            conn
        );

        cmd.Parameters.AddWithValue("job_id", jobId);
        cmd.Parameters.AddWithValue("job_type_key", "project.archive");
        cmd.Parameters.AddWithValue("payload", payloadJson);
        cmd.Parameters.AddWithValue("entity_type_key", "project");
        cmd.Parameters.AddWithValue("entity_key", payload.ProjectId);

        await cmd.ExecuteNonQueryAsync();

        Console.WriteLine($"archive: enqueued job_id={jobId} project_id={payload.ProjectId}");
        Console.WriteLine($"archive: payload={payloadJson}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"archive: enqueue failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> EnqueueRootIntegrityAsync(RootIntegrityJobPayload payload)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await EnsureRootIntegrityJobTypeExistsAsync(conn);

        var jobId = EntityIds.NewWithPrefix("job");
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO public.jobs (job_id, job_type_key, payload, status_key, run_after, entity_type_key, entity_key)
            VALUES (@job_id, @job_type_key, @payload::jsonb, 'queued', now(), @entity_type_key, @entity_key);
            """,
            conn
        );

        cmd.Parameters.AddWithValue("job_id", jobId);
        cmd.Parameters.AddWithValue("job_type_key", "domain.root_integrity");
        cmd.Parameters.AddWithValue("payload", payloadJson);
        cmd.Parameters.AddWithValue("entity_type_key", "storage_root");
        cmd.Parameters.AddWithValue("entity_key", $"{payload.ProviderKey}:{payload.RootKey}");

        await cmd.ExecuteNonQueryAsync();

        Console.WriteLine($"root-integrity: enqueued job_id={jobId} provider={payload.ProviderKey} root_key={payload.RootKey}");
        Console.WriteLine($"root-integrity: payload={payloadJson}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"root-integrity: enqueue failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> MarkReadyAsync(string projectId)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            """
            UPDATE public.projects
            SET status_key = 'ready_to_provision',
                updated_at = now()
            WHERE project_id = @project_id;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("project_id", projectId);

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0)
        {
            Console.Error.WriteLine($"bootstrap: project not found: {projectId}");
            return 1;
        }

        Console.WriteLine($"bootstrap: project marked ready_to_provision (project_id={projectId})");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: ready failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> MarkToArchiveAsync(string projectId)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            """
            UPDATE public.projects
            SET status_key = 'to_archive',
                updated_at = now()
            WHERE project_id = @project_id;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("project_id", projectId);

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0)
        {
            Console.Error.WriteLine($"archive: project not found: {projectId}");
            return 1;
        }

        Console.WriteLine($"archive: project marked to_archive (project_id={projectId})");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"archive: to-archive failed: {ex.Message}");
        return 1;
    }
}

static async Task EnsureJobTypeExistsAsync(NpgsqlConnection conn)
{
    await using var cmd = new NpgsqlCommand(
        """
        INSERT INTO public.job_types (job_type_key, display_name)
        VALUES (@job_type_key, @display_name)
        ON CONFLICT (job_type_key) DO UPDATE
        SET display_name = EXCLUDED.display_name;
        """,
        conn
    );

    cmd.Parameters.AddWithValue("job_type_key", "project.bootstrap");
    cmd.Parameters.AddWithValue("display_name", "Project: Bootstrap");
    await cmd.ExecuteNonQueryAsync();
}

static async Task EnsureArchiveJobTypeExistsAsync(NpgsqlConnection conn)
{
    await using var cmd = new NpgsqlCommand(
        """
        INSERT INTO public.job_types (job_type_key, display_name)
        VALUES (@job_type_key, @display_name)
        ON CONFLICT (job_type_key) DO UPDATE
        SET display_name = EXCLUDED.display_name;
        """,
        conn
    );

    cmd.Parameters.AddWithValue("job_type_key", "project.archive");
    cmd.Parameters.AddWithValue("display_name", "Project: Archive");
    await cmd.ExecuteNonQueryAsync();
}

static async Task EnsureRootIntegrityJobTypeExistsAsync(NpgsqlConnection conn)
{
    await using var cmd = new NpgsqlCommand(
        """
        INSERT INTO public.job_types (job_type_key, display_name)
        VALUES (@job_type_key, @display_name)
        ON CONFLICT (job_type_key) DO UPDATE
        SET display_name = EXCLUDED.display_name;
        """,
        conn
    );

    cmd.Parameters.AddWithValue("job_type_key", "domain.root_integrity");
    cmd.Parameters.AddWithValue("display_name", "Domain: Root Integrity Check");
    await cmd.ExecuteNonQueryAsync();
}

static async Task<ExistingJob?> FindExistingJobAsync(NpgsqlConnection conn, string projectId)
{
    await using var cmd = new NpgsqlCommand(
        """
        SELECT job_id, status_key
        FROM public.jobs
        WHERE job_type_key = 'project.bootstrap'
          AND entity_type_key = 'project'
          AND entity_key = @project_id
          AND status_key IN ('queued', 'running')
        ORDER BY created_at DESC
        LIMIT 1;
        """,
        conn
    );

    cmd.Parameters.AddWithValue("project_id", projectId);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return null;
    }

    return new ExistingJob(reader.GetString(0), reader.GetString(1));
}

static async Task<ExistingJob?> FindExistingArchiveJobAsync(NpgsqlConnection conn, string projectId)
{
    await using var cmd = new NpgsqlCommand(
        """
        SELECT job_id, status_key
        FROM public.jobs
        WHERE job_type_key = 'project.archive'
          AND entity_type_key = 'project'
          AND entity_key = @project_id
          AND status_key IN ('queued', 'running')
        ORDER BY created_at DESC
        LIMIT 1;
        """,
        conn
    );

    cmd.Parameters.AddWithValue("project_id", projectId);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return null;
    }

    return new ExistingJob(reader.GetString(0), reader.GetString(1));
}

static async Task<int> ShowProjectAsync(string projectId)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        string projectCode;
        string name;
        string statusKey;
        string dataProfile;
        string metadataJson;

        await using (var cmd = new NpgsqlCommand(
                         """
                         SELECT project_id, project_code, name, status_key, data_profile, metadata::text
                         FROM public.projects
                         WHERE project_id = @project_id;
                         """,
                         conn
                     ))
        {
            cmd.Parameters.AddWithValue("project_id", projectId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                Console.Error.WriteLine($"bootstrap: project not found: {projectId}");
                return 1;
            }

            projectCode = reader.GetString(1);
            name = reader.GetString(2);
            statusKey = reader.GetString(3);
            dataProfile = reader.GetString(4);
            metadataJson = reader.GetString(5);
        }

        Console.WriteLine($"project_id={projectId}");
        Console.WriteLine($"project_code={projectCode}");
        Console.WriteLine($"name={name}");
        Console.WriteLine($"status_key={statusKey}");
        Console.WriteLine($"data_profile={dataProfile}");

        var pretty = JsonSerializer.Serialize(
            JsonDocument.Parse(metadataJson).RootElement,
            new JsonSerializerOptions { WriteIndented = true }
        );
        Console.WriteLine("metadata:");
        Console.WriteLine(pretty);

        await using (var rootsCmd = new NpgsqlCommand(
                         """
                         SELECT project_storage_root_id,
                                storage_provider_key,
                                root_key,
                                folder_relpath,
                                is_primary,
                                created_at
                         FROM public.project_storage_roots
                         WHERE project_id = @project_id
                         ORDER BY created_at DESC;
                         """,
                         conn
                     ))
        {
            rootsCmd.Parameters.AddWithValue("project_id", projectId);

            await using var rootsReader = await rootsCmd.ExecuteReaderAsync();
            Console.WriteLine("storage_roots:");
            var hadRows = false;
            while (await rootsReader.ReadAsync())
            {
                hadRows = true;
                var rootId = rootsReader.GetString(0);
                var provider = rootsReader.GetString(1);
                var rootKey = rootsReader.GetString(2);
                var relpath = rootsReader.GetString(3);
                var isPrimary = rootsReader.GetBoolean(4);
                var createdAt = rootsReader.GetFieldValue<DateTimeOffset>(5);
                Console.WriteLine($"- {rootId}\t{provider}\t{rootKey}\t{relpath}\tis_primary={isPrimary}\tcreated_at={createdAt:O}");
            }

            if (!hadRows)
            {
                Console.WriteLine("- (none)");
            }
        }

        await using (var jobsCmd = new NpgsqlCommand(
                         """
                         SELECT job_id, status_key, attempt_count, run_after, locked_until
                         FROM public.jobs
                         WHERE job_type_key = 'project.bootstrap'
                           AND entity_type_key = 'project'
                           AND entity_key = @project_id
                         ORDER BY created_at DESC;
                         """,
                         conn
                     ))
        {
            jobsCmd.Parameters.AddWithValue("project_id", projectId);

            await using var jobsReader = await jobsCmd.ExecuteReaderAsync();
            Console.WriteLine("bootstrap_jobs:");
            var hadJobs = false;
            while (await jobsReader.ReadAsync())
            {
                hadJobs = true;
                var jobId = jobsReader.GetString(0);
                var status = jobsReader.GetString(1);
                var attempt = jobsReader.GetInt32(2);
                var runAfter = jobsReader.GetFieldValue<DateTimeOffset>(3);
                var lockedUntil = jobsReader.IsDBNull(4) ? (DateTimeOffset?)null : jobsReader.GetFieldValue<DateTimeOffset>(4);
                Console.WriteLine($"- {jobId}\t{status}\tattempts={attempt}\trun_after={runAfter:O}\tlocked_until={(lockedUntil.HasValue ? lockedUntil.Value.ToString("O") : "(null)")}");
            }

            if (!hadJobs)
            {
                Console.WriteLine("- (none)");
            }
        }

        await using (var archiveJobsCmd = new NpgsqlCommand(
                         """
                         SELECT job_id, status_key, attempt_count, run_after, locked_until
                         FROM public.jobs
                         WHERE job_type_key = 'project.archive'
                           AND entity_type_key = 'project'
                           AND entity_key = @project_id
                         ORDER BY created_at DESC;
                         """,
                         conn
                     ))
        {
            archiveJobsCmd.Parameters.AddWithValue("project_id", projectId);

            await using var archiveReader = await archiveJobsCmd.ExecuteReaderAsync();
            Console.WriteLine("archive_jobs:");
            var hadArchiveJobs = false;
            while (await archiveReader.ReadAsync())
            {
                hadArchiveJobs = true;
                var jobId = archiveReader.GetString(0);
                var status = archiveReader.GetString(1);
                var attempt = archiveReader.GetInt32(2);
                var runAfter = archiveReader.GetFieldValue<DateTimeOffset>(3);
                var lockedUntil = archiveReader.IsDBNull(4) ? (DateTimeOffset?)null : archiveReader.GetFieldValue<DateTimeOffset>(4);
                Console.WriteLine($"- {jobId}\t{status}\tattempts={attempt}\trun_after={runAfter:O}\tlocked_until={(lockedUntil.HasValue ? lockedUntil.Value.ToString("O") : "(null)")}");
            }

            if (!hadArchiveJobs)
            {
                Console.WriteLine("- (none)");
            }
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: show failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> ShowRootIntegrityAsync(string provider, string rootKey)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            """
            SELECT job_id, status_key, attempt_count, run_after, locked_until, payload::text
            FROM public.jobs
            WHERE job_type_key = 'domain.root_integrity'
              AND entity_type_key = 'storage_root'
              AND entity_key = @entity_key
            ORDER BY created_at DESC
            LIMIT 5;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("entity_key", $"{provider}:{rootKey}");

        await using var reader = await cmd.ExecuteReaderAsync();
        Console.WriteLine($"root_integrity_jobs ({provider}:{rootKey}):");

        var hadRows = false;
        while (await reader.ReadAsync())
        {
            hadRows = true;
            var jobId = reader.GetString(0);
            var status = reader.GetString(1);
            var attempt = reader.GetInt32(2);
            var runAfter = reader.GetFieldValue<DateTimeOffset>(3);
            var lockedUntil = reader.IsDBNull(4) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(4);
            var payloadJson = reader.GetString(5);

            var summary = SummarizeRootIntegrityPayload(payloadJson);
            Console.WriteLine($"- {jobId}\t{status}\tattempts={attempt}\trun_after={runAfter:O}\tlocked_until={(lockedUntil.HasValue ? lockedUntil.Value.ToString("O") : "(null)")} {summary}");
        }

        if (!hadRows)
        {
            Console.WriteLine("- (none)");
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"root-integrity: show failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> ListProjectsAsync(int limit)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            """
            SELECT project_id, project_code, name
            FROM public.projects
            ORDER BY created_at DESC
            LIMIT @limit;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Console.WriteLine($"{reader.GetString(0)}\t{reader.GetString(1)}\t{reader.GetString(2)}");
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: list failed: {ex.Message}");
        return 1;
    }
}

static string SummarizeRootIntegrityPayload(string payloadJson)
{
    try
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var provider = root.TryGetProperty("providerKey", out var providerElement) ? providerElement.GetString() : null;
        var rootKey = root.TryGetProperty("rootKey", out var rootKeyElement) ? rootKeyElement.GetString() : null;
        var mode = root.TryGetProperty("mode", out var modeElement) ? modeElement.GetString() : null;
        var dryRun = root.TryGetProperty("dryRun", out var dryRunElement) && dryRunElement.ValueKind == JsonValueKind.True
            ? "dryRun=true"
            : "dryRun=false";

        if (!root.TryGetProperty("result", out var resultElement))
        {
            return $"provider={provider} root_key={rootKey} mode={mode} {dryRun}";
        }

        var missingRequired = resultElement.TryGetProperty("missingRequired", out var missingElement) && missingElement.ValueKind == JsonValueKind.Array
            ? missingElement.GetArrayLength()
            : 0;
        var unknown = resultElement.TryGetProperty("unknownEntries", out var unknownElement) && unknownElement.ValueKind == JsonValueKind.Array
            ? unknownElement.GetArrayLength()
            : 0;
        var errors = resultElement.TryGetProperty("errors", out var errorElement) && errorElement.ValueKind == JsonValueKind.Array
            ? errorElement.GetArrayLength()
            : 0;

        return $"provider={provider} root_key={rootKey} mode={mode} {dryRun} missing_required={missingRequired} unknown={unknown} errors={errors}";
    }
    catch
    {
        return "(payload parse failed)";
    }
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
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        if (!forceNew)
        {
            var existing = await FindTestProjectAsync(conn, testKey);
            if (existing is not null)
            {
                Console.WriteLine("bootstrap: existing test project found (use --forceNew to create another)");
                Console.WriteLine($"project_id={existing.ProjectId}");
                Console.WriteLine($"project_code={existing.ProjectCode}");
                Console.WriteLine($"name={existing.ProjectName}");
                Console.WriteLine($"client_id={existing.ClientId}");
                return 0;
            }
        }

        await using var tx = await conn.BeginTransactionAsync();

        var personId = EntityIds.NewPersonId();
        var clientId = EntityIds.NewClientId();
        var projectId = EntityIds.NewProjectId();
        var projectMemberId = EntityIds.NewWithPrefix("prm");

        var projectCode = await AllocateProjectCodeAsync(conn, tx);
        var metadataJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["test_key"] = testKey,
            ["test_type"] = "bootstrap",
            ["created_by"] = "MGF.Tools.ProjectBootstrap",
            ["created_at"] = DateTimeOffset.UtcNow.ToString("O")
        });

        await using (var personCmd = new NpgsqlCommand(
                         """
                         INSERT INTO public.people (person_id, first_name, last_name, initials, status_key, data_profile)
                         VALUES (@person_id, @first_name, @last_name, @initials, 'active', 'real');
                         """,
                         conn,
                         tx
                     ))
        {
            personCmd.Parameters.AddWithValue("person_id", personId);
            personCmd.Parameters.AddWithValue("first_name", editorFirstName);
            personCmd.Parameters.AddWithValue("last_name", editorLastName);
            personCmd.Parameters.AddWithValue("initials", editorInitials);
            await personCmd.ExecuteNonQueryAsync();
        }

        await using (var clientCmd = new NpgsqlCommand(
                         """
                         INSERT INTO public.clients (client_id, display_name, client_type_key, status_key, data_profile, primary_contact_person_id)
                         VALUES (@client_id, @display_name, 'organization', 'active', 'real', @primary_contact_person_id);
                         """,
                         conn,
                         tx
                     ))
        {
            clientCmd.Parameters.AddWithValue("client_id", clientId);
            clientCmd.Parameters.AddWithValue("display_name", clientName);
            clientCmd.Parameters.AddWithValue("primary_contact_person_id", personId);
            await clientCmd.ExecuteNonQueryAsync();
        }

        await using (var roleCmd = new NpgsqlCommand(
                         """
                         INSERT INTO public.person_roles (person_id, role_key)
                         VALUES (@person_id, 'editor')
                         ON CONFLICT (person_id, role_key) DO NOTHING;
                         """,
                         conn,
                         tx
                     ))
        {
            roleCmd.Parameters.AddWithValue("person_id", personId);
            await roleCmd.ExecuteNonQueryAsync();
        }

        await using (var projectCmd = new NpgsqlCommand(
                         """
                         INSERT INTO public.projects (project_id, project_code, client_id, name, status_key, phase_key, data_profile, metadata)
                         VALUES (@project_id, @project_code, @client_id, @name, 'active', 'planning', 'real', @metadata::jsonb);
                         """,
                         conn,
                         tx
                     ))
        {
            projectCmd.Parameters.AddWithValue("project_id", projectId);
            projectCmd.Parameters.AddWithValue("project_code", projectCode);
            projectCmd.Parameters.AddWithValue("client_id", clientId);
            projectCmd.Parameters.AddWithValue("name", projectName);
            projectCmd.Parameters.AddWithValue("metadata", metadataJson);
            await projectCmd.ExecuteNonQueryAsync();
        }

        await using (var memberCmd = new NpgsqlCommand(
                         """
                         INSERT INTO public.project_members (project_member_id, project_id, person_id, role_key, assigned_at)
                         VALUES (@project_member_id, @project_id, @person_id, 'editor', now());
                         """,
                         conn,
                         tx
                     ))
        {
            memberCmd.Parameters.AddWithValue("project_member_id", projectMemberId);
            memberCmd.Parameters.AddWithValue("project_id", projectId);
            memberCmd.Parameters.AddWithValue("person_id", personId);
            await memberCmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();

        Console.WriteLine("bootstrap: created test project");
        Console.WriteLine($"project_id={projectId}");
        Console.WriteLine($"project_code={projectCode}");
        Console.WriteLine($"name={projectName}");
        Console.WriteLine($"client_id={clientId}");
        Console.WriteLine($"person_id={personId}");
        Console.WriteLine($"editor_initials={editorInitials}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: create-test failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> ResetJobsAsync(string projectId, string jobTypeKey)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            """
            UPDATE public.jobs
            SET status_key = 'queued',
                run_after = now(),
                locked_by = NULL,
                locked_until = NULL
            WHERE job_type_key = @job_type_key
              AND entity_type_key = 'project'
              AND entity_key = @project_id
              AND status_key IN ('queued','running');
            """,
            conn
        );

        cmd.Parameters.AddWithValue("project_id", projectId);
        cmd.Parameters.AddWithValue("job_type_key", jobTypeKey);

        var rows = await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"bootstrap: reset {rows} job(s) for project_id={projectId} (job_type_key={jobTypeKey})");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: reset-jobs failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> RequeueStaleJobsAsync(bool dryRun)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        if (dryRun)
        {
            await using var countCmd = new NpgsqlCommand(
                """
                SELECT count(*)
                FROM public.jobs
                WHERE status_key = 'running'
                  AND (
                    (locked_until IS NOT NULL AND locked_until < now())
                    OR (
                      locked_until IS NULL
                      AND started_at IS NOT NULL
                      AND started_at < now() - interval '60 minutes'
                    )
                  );
                """,
                conn
            );

            var result = await countCmd.ExecuteScalarAsync();
            var count = result is null || result is DBNull ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
            Console.WriteLine($"bootstrap: stale running jobs (dry-run) count={count}");
            return 0;
        }

        await using var cmd = new NpgsqlCommand(
            """
            WITH reset AS (
              UPDATE public.jobs
              SET status_key = 'queued',
                  run_after = now(),
                  locked_by = NULL,
                  locked_until = NULL,
                  last_error = CASE
                    WHEN locked_until IS NULL
                      THEN 'reaped stale running job (no lock, started_at stale)'
                    ELSE 'reaped stale running job (expired lock)'
                  END
              WHERE status_key = 'running'
                AND (
                  (locked_until IS NOT NULL AND locked_until < now())
                  OR (
                    locked_until IS NULL
                    AND started_at IS NOT NULL
                    AND started_at < now() - interval '60 minutes'
                  )
                )
              RETURNING 1
            )
            SELECT count(*) FROM reset;
            """,
            conn
        );

        var updated = await cmd.ExecuteScalarAsync();
        var countUpdated = updated is null || updated is DBNull ? 0 : Convert.ToInt32(updated, CultureInfo.InvariantCulture);
        Console.WriteLine($"bootstrap: requeued stale running jobs count={countUpdated}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: jobs-requeue-stale failed: {ex.Message}");
        return 1;
    }
}

static async Task<TestProjectInfo?> FindTestProjectAsync(NpgsqlConnection conn, string testKey)
{
    await using var cmd = new NpgsqlCommand(
        """
        SELECT project_id, project_code, name, client_id
        FROM public.projects
        WHERE metadata->>'test_key' = @test_key
        ORDER BY created_at DESC
        LIMIT 1;
        """,
        conn
    );

    cmd.Parameters.AddWithValue("test_key", testKey);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return null;
    }

    return new TestProjectInfo(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3)
    );
}

static async Task<string> AllocateProjectCodeAsync(NpgsqlConnection conn, NpgsqlTransaction tx)
{
    await using var cmd = new NpgsqlCommand(
        """
        WITH ensured AS (
          INSERT INTO public.project_code_counters(prefix, year_2, next_seq)
          VALUES ('MGF', (EXTRACT(YEAR FROM now())::int % 100)::smallint, 1)
          ON CONFLICT (prefix, year_2) DO NOTHING
        ),
        updated AS (
          UPDATE public.project_code_counters
          SET next_seq = next_seq + 1, updated_at = now()
          WHERE prefix = 'MGF' AND year_2 = (EXTRACT(YEAR FROM now())::int % 100)::smallint
          RETURNING year_2, (next_seq - 1) AS allocated_seq
        )
        SELECT 'MGF' || lpad(year_2::text, 2, '0') || '-' || lpad(allocated_seq::text, 4, '0')
        FROM updated;
        """,
        conn,
        tx
    );

    var result = await cmd.ExecuteScalarAsync();
    return result?.ToString() ?? throw new InvalidOperationException("Failed to allocate project code.");
}

static IConfiguration BuildConfiguration()
{
    var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? "Development";

    var builder = new ConfigurationBuilder();
    builder.AddMgfConfiguration(env, typeof(AppDbContext).Assembly);
    return builder.Build();
}

static IReadOnlyList<string> ParseEditors(IEnumerable<string> editors)
{
    return editors
        .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

sealed record ProjectBootstrapJobPayload(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    bool VerifyDomainRoots,
    bool CreateDomainRoots,
    bool ProvisionProjectContainers,
    bool AllowRepair,
    bool ForceSandbox,
    bool AllowNonReal,
    bool Force,
    bool TestMode,
    bool AllowTestCleanup
);

sealed record ProjectArchiveJobPayload(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    bool TestMode,
    bool AllowTestCleanup,
    bool AllowNonReal,
    bool Force
);

sealed record RootIntegrityJobPayload(
    string ProviderKey,
    string RootKey,
    string Mode,
    bool DryRun,
    string? QuarantineRelpath,
    int? MaxItems,
    long? MaxBytes
);

sealed record ExistingJob(string JobId, string StatusKey);

sealed record TestProjectInfo(string ProjectId, string ProjectCode, string ProjectName, string ClientId);
