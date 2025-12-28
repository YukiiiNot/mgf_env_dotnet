using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MGF.Domain.Entities;
using MGF.Infrastructure.Configuration;
using MGF.Infrastructure.Data;
using Npgsql;

var root = new RootCommand("MGF Project Bootstrap Job Tool");
root.AddCommand(CreateEnqueueCommand());
root.AddCommand(CreateReadyCommand());
root.AddCommand(CreateShowCommand());
root.AddCommand(CreateListCommand());

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

static async Task<int> ShowProjectAsync(string projectId)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            """
            SELECT project_id, project_code, name, status_key, data_profile, metadata::text
            FROM public.projects
            WHERE project_id = @project_id;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            Console.Error.WriteLine($"bootstrap: project not found: {projectId}");
            return 1;
        }

        var projectCode = reader.GetString(1);
        var name = reader.GetString(2);
        var statusKey = reader.GetString(3);
        var dataProfile = reader.GetString(4);
        var metadataJson = reader.GetString(5);

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
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: show failed: {ex.Message}");
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

sealed record ExistingJob(string JobId, string StatusKey);
