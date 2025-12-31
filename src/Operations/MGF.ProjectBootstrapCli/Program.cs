using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MGF.Domain.Entities;
using MGF.Infrastructure.Configuration;
using MGF.Infrastructure.Data;
using MGF.Tools.Provisioner;
using MGF.Tools.ProjectBootstrap;
using MGF.Worker.Email.Composition;
using MGF.Worker.Email.Models;
using MGF.Worker.Email.Registry;
using MGF.Worker.ProjectDelivery;
using Npgsql;

var root = new RootCommand("MGF Project Bootstrap Job Tool");
root.AddCommand(CreateEnqueueCommand());
root.AddCommand(CreateReadyCommand());
root.AddCommand(CreateToArchiveCommand());
root.AddCommand(CreateArchiveCommand());
root.AddCommand(CreateToDeliverCommand());
root.AddCommand(CreateDeliverCommand());
root.AddCommand(CreateDeliveryEmailCommand());
root.AddCommand(CreateEmailPreviewCommand());
root.AddCommand(CreateSeedDeliverablesCommand());
root.AddCommand(CreateRootAuditCommand());
root.AddCommand(CreateRootRepairCommand());
root.AddCommand(CreateRootShowCommand());
root.AddCommand(CreateDevTestCleanCommand());
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

static Command CreateToDeliverCommand()
{
    var command = new Command("to-deliver", "mark a project as ready_to_deliver.");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID to mark ready_to_deliver.",
        IsRequired = true
    };

    command.AddOption(projectIdOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var exitCode = await MarkToDeliverAsync(projectId);
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

    var editorInitialsOption = new Option<string>("--editorInitials")
    {
        Description = "Comma-separated editor initials (e.g., ER,AB).",
        Arity = ArgumentArity.ZeroOrOne
    };
    editorInitialsOption.SetDefaultValue("TE");

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
    command.AddOption(editorInitialsOption);
    command.AddOption(testModeOption);
    command.AddOption(allowTestCleanupOption);
    command.AddOption(allowNonRealOption);
    command.AddOption(forceOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var editorInitials = context.ParseResult.GetValueForOption(editorInitialsOption) ?? "TE";
        var payload = new ProjectArchiveJobPayload(
            ProjectId: projectId,
            EditorInitials: ParseEditors(new[] { editorInitials }),
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

static Command CreateDeliverCommand()
{
    var command = new Command("deliver", "enqueue a project.delivery job.");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID to deliver (e.g., prj_...).",
        IsRequired = true
    };

    var editorInitialsOption = new Option<string>("--editorInitials")
    {
        Description = "Comma-separated editor initials (e.g., ER,AB).",
        Arity = ArgumentArity.ZeroOrOne
    };
    editorInitialsOption.SetDefaultValue("TE");

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
        Description = "Allow delivery for non-real data_profile projects (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var forceOption = new Option<bool?>("--force")
    {
        Description = "Force delivery even if project status is not ready_to_deliver (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var refreshShareLinkOption = new Option<bool?>("--refreshShareLink")
    {
        Description = "Refresh/recreate the Dropbox stable share link (default: false).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var toOption = new Option<string[]>("--to")
    {
        Description = "Delivery email recipient(s), comma-separated or repeatable.",
        Arity = ArgumentArity.ZeroOrMore
    };
    toOption.AllowMultipleArgumentsPerToken = true;

    var replyToOption = new Option<string>("--replyTo")
    {
        Description = "Optional reply-to email address for delivery emails.",
        Arity = ArgumentArity.ZeroOrOne
    };

    command.AddOption(projectIdOption);
    command.AddOption(editorInitialsOption);
    command.AddOption(testModeOption);
    command.AddOption(allowTestCleanupOption);
    command.AddOption(allowNonRealOption);
    command.AddOption(forceOption);
    command.AddOption(refreshShareLinkOption);
    command.AddOption(toOption);
    command.AddOption(replyToOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var editorInitials = context.ParseResult.GetValueForOption(editorInitialsOption) ?? "TE";
        var payload = new ProjectDeliveryJobPayload(
            ProjectId: projectId,
            EditorInitials: ParseEditors(new[] { editorInitials }),
            ToEmails: ParseEmails(context.ParseResult.GetValueForOption(toOption) ?? Array.Empty<string>()),
            ReplyToEmail: context.ParseResult.GetValueForOption(replyToOption),
            TestMode: context.ParseResult.GetValueForOption(testModeOption) ?? false,
            AllowTestCleanup: context.ParseResult.GetValueForOption(allowTestCleanupOption) ?? false,
            AllowNonReal: context.ParseResult.GetValueForOption(allowNonRealOption) ?? false,
            Force: context.ParseResult.GetValueForOption(forceOption) ?? false,
            RefreshShareLink: context.ParseResult.GetValueForOption(refreshShareLinkOption) ?? false
        );

        var exitCode = await EnqueueDeliveryAsync(payload);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateDeliveryEmailCommand()
{
    var command = new Command("delivery-email", "send/resend delivery email without copying deliverables.");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID to email (e.g., prj_...).",
        IsRequired = true
    };

    var editorInitialsOption = new Option<string>("--editorInitials")
    {
        Description = "Comma-separated editor initials (e.g., ER,AB).",
        Arity = ArgumentArity.ZeroOrOne
    };
    editorInitialsOption.SetDefaultValue("TE");

    var toOption = new Option<string[]>("--to")
    {
        Description = "Delivery email recipient(s), comma-separated or repeatable.",
        Arity = ArgumentArity.OneOrMore,
        IsRequired = true
    };
    toOption.AllowMultipleArgumentsPerToken = true;

    var replyToOption = new Option<string>("--replyTo")
    {
        Description = "Optional reply-to email address.",
        Arity = ArgumentArity.ZeroOrOne
    };
    var fromOption = new Option<string>("--from")
    {
        Description = "Optional from address (must be deliveries@mgfilms.pro if provided).",
        Arity = ArgumentArity.ZeroOrOne
    };

    command.AddOption(projectIdOption);
    command.AddOption(editorInitialsOption);
    command.AddOption(toOption);
    command.AddOption(replyToOption);
    command.AddOption(fromOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var editorInitials = context.ParseResult.GetValueForOption(editorInitialsOption) ?? "TE";
        var fromValue = context.ParseResult.GetValueForOption(fromOption);
        if (!string.IsNullOrWhiteSpace(fromValue)
            && !string.Equals(fromValue, "deliveries@mgfilms.pro", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fromValue, "info@mgfilms.pro", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("bootstrap: delivery-email --from must be deliveries@mgfilms.pro or info@mgfilms.pro");
            context.ExitCode = 1;
            return;
        }
        var payload = new ProjectDeliveryEmailJobPayload(
            ProjectId: projectId,
            EditorInitials: ParseEditors(new[] { editorInitials }),
            ToEmails: ParseEmails(context.ParseResult.GetValueForOption(toOption) ?? Array.Empty<string>()),
            ReplyToEmail: context.ParseResult.GetValueForOption(replyToOption)
        );

        var exitCode = await EnqueueDeliveryEmailAsync(payload);
        context.ExitCode = exitCode;
    });

    return command;
}

static Command CreateEmailPreviewCommand()
{
    var command = new Command("email-preview", "render a delivery-ready email to disk without sending.");

    var kindOption = new Option<string>("--kind")
    {
        Description = "Email kind (delivery_ready only for now).",
        IsRequired = false
    };
    kindOption.SetDefaultValue("delivery_ready");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID to preview (e.g., prj_...).",
        IsRequired = false
    };

    var fixtureOption = new Option<string>("--fixture")
    {
        Description = "Fixture name or path (basic, large_files, no_logo) for preview without a project.",
        IsRequired = false
    };

    var outOption = new Option<string>("--out")
    {
        Description = "Output directory for preview.html/preview.txt/preview.json.",
        IsRequired = true
    };

    var writeSnapshotsOption = new Option<bool?>("--writeSnapshots")
    {
        Description = "Write golden HTML snapshot for the fixture (requires --fixture).",
        Arity = ArgumentArity.ZeroOrOne
    };

    var snapshotOutOption = new Option<string?>("--snapshotOut")
    {
        Description = "Output directory for snapshot HTML when --writeSnapshots is set (dev-only).",
        IsRequired = false
    };

    var toOption = new Option<string[]>("--to")
    {
        Description = "Preview recipient(s) (optional; default info@mgfilms.pro).",
        Arity = ArgumentArity.ZeroOrMore
    };
    toOption.AllowMultipleArgumentsPerToken = true;

    command.AddOption(kindOption);
    command.AddOption(projectIdOption);
    command.AddOption(fixtureOption);
    command.AddOption(outOption);
    command.AddOption(writeSnapshotsOption);
    command.AddOption(snapshotOutOption);
    command.AddOption(toOption);

    command.SetHandler(async context =>
    {
        var kind = (context.ParseResult.GetValueForOption(kindOption) ?? "delivery_ready").Trim();
        if (!kind.Equals("delivery_ready", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"bootstrap: email-preview kind '{kind}' is not supported.");
            context.ExitCode = 1;
            return;
        }

        var outDir = context.ParseResult.GetValueForOption(outOption) ?? string.Empty;
        var fixture = context.ParseResult.GetValueForOption(fixtureOption);
        var writeSnapshots = context.ParseResult.GetValueForOption(writeSnapshotsOption) ?? false;
        var snapshotOut = context.ParseResult.GetValueForOption(snapshotOutOption);
        var toEmails = ParseEmails(context.ParseResult.GetValueForOption(toOption) ?? Array.Empty<string>());
        if (toEmails.Count == 0)
        {
            toEmails = new[] { "info@mgfilms.pro" };
        }

        int exitCode;
        if (!string.IsNullOrWhiteSpace(fixture))
        {
            if (writeSnapshots && string.IsNullOrWhiteSpace(snapshotOut))
            {
                Console.Error.WriteLine("bootstrap: email-preview --writeSnapshots requires --snapshotOut.");
                context.ExitCode = 1;
                return;
            }

            exitCode = await RenderDeliveryPreviewFromFixtureAsync(fixture, outDir, toEmails, writeSnapshots, snapshotOut);
        }
        else
        {
            if (writeSnapshots)
            {
                Console.Error.WriteLine("bootstrap: email-preview --writeSnapshots requires --fixture.");
                context.ExitCode = 1;
                return;
            }

            var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(projectId))
            {
                Console.Error.WriteLine("bootstrap: email-preview requires either --projectId or --fixture.");
                context.ExitCode = 1;
                return;
            }

            exitCode = await RenderDeliveryPreviewAsync(projectId, outDir, toEmails);
        }
        context.ExitCode = exitCode;
    });

    return command;
}

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

static Command CreateShowCommand()
{
    var command = new Command("show", "show project metadata for a project id.");

    var projectIdOption = new Option<string>("--projectId")
    {
        Description = "Project ID to inspect.",
        IsRequired = true
    };
    var jobsOnlyOption = new Option<bool>("--jobsOnly")
    {
        Description = "Print only job tables and last delivery email summary.",
        IsRequired = false
    };
    jobsOnlyOption.SetDefaultValue(false);

    command.AddOption(projectIdOption);
    command.AddOption(jobsOnlyOption);

    command.SetHandler(async context =>
    {
        var projectId = context.ParseResult.GetValueForOption(projectIdOption) ?? string.Empty;
        var jobsOnly = context.ParseResult.GetValueForOption(jobsOnlyOption);
        var exitCode = await ShowProjectAsync(projectId, jobsOnly);
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

static async Task<int> EnqueueDeliveryAsync(ProjectDeliveryJobPayload payload)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var existingJob = await FindExistingDeliveryJobAsync(conn, payload.ProjectId);
        if (!DeliveryJobGuard.ShouldEnqueue(existingJob, out var reason))
        {
            Console.Error.WriteLine($"bootstrap: delivery enqueue blocked: {reason}");
            return 2;
        }

        var jobId = EntityIds.NewWithPrefix("job");
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO public.jobs (
                job_id,
                job_type_key,
                status_key,
                attempt_count,
                max_attempts,
                run_after,
                payload,
                entity_type_key,
                entity_key
            )
            VALUES (
                @job_id,
                'project.delivery',
                'queued',
                0,
                5,
                now(),
                @payload::jsonb,
                'project',
                @project_id
            );
            """,
            conn
        );

        cmd.Parameters.AddWithValue("job_id", jobId);
        cmd.Parameters.AddWithValue("payload", json);
        cmd.Parameters.AddWithValue("project_id", payload.ProjectId);

        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"bootstrap: project.delivery enqueued job_id={jobId} project_id={payload.ProjectId}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: delivery enqueue failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> EnqueueDeliveryEmailAsync(ProjectDeliveryEmailJobPayload payload)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var existingJob = await FindExistingDeliveryEmailJobAsync(conn, payload.ProjectId);
        if (!DeliveryEmailJobGuard.ShouldEnqueue(existingJob, out var reason))
        {
            Console.Error.WriteLine($"bootstrap: delivery-email enqueue blocked: {reason}");
            return 2;
        }

        var jobId = EntityIds.NewWithPrefix("job");
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO public.jobs (
                job_id,
                job_type_key,
                status_key,
                attempt_count,
                max_attempts,
                run_after,
                payload,
                entity_type_key,
                entity_key
            )
            VALUES (
                @job_id,
                'project.delivery_email',
                'queued',
                0,
                5,
                now(),
                @payload::jsonb,
                'project',
                @project_id
            );
            """,
            conn
        );

        cmd.Parameters.AddWithValue("job_id", jobId);
        cmd.Parameters.AddWithValue("payload", json);
        cmd.Parameters.AddWithValue("project_id", payload.ProjectId);

        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"bootstrap: project.delivery_email enqueued job_id={jobId} project_id={payload.ProjectId}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: delivery-email enqueue failed: {ex.Message}");
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

static async Task<int> MarkToDeliverAsync(string projectId)
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
            SET status_key = 'ready_to_deliver',
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

        Console.WriteLine($"bootstrap: project marked ready_to_deliver: {projectId}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: ready_to_deliver failed: {ex.Message}");
        return 1;
    }
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

static async Task<int> ShowProjectAsync(string projectId, bool jobsOnly)
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

        if (!jobsOnly)
        {
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
        }
        else
        {
            PrintLastEmailSummary(metadataJson);
        }

        if (!jobsOnly)
        {
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
            var jobs = new List<(string JobId, string Status, int AttemptCount, DateTimeOffset RunAfter, DateTimeOffset? LockedUntil)>();
            while (await jobsReader.ReadAsync())
            {
                var jobId = jobsReader.GetString(0);
                var status = jobsReader.GetString(1);
                var attempt = jobsReader.GetInt32(2);
                var runAfter = jobsReader.GetFieldValue<DateTimeOffset>(3);
                var lockedUntil = jobsReader.IsDBNull(4) ? (DateTimeOffset?)null : jobsReader.GetFieldValue<DateTimeOffset>(4);
                jobs.Add((jobId, status, attempt, runAfter, lockedUntil));
            }
            PrintJobTable("bootstrap_jobs", jobs);
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
            var jobs = new List<(string JobId, string Status, int AttemptCount, DateTimeOffset RunAfter, DateTimeOffset? LockedUntil)>();
            while (await archiveReader.ReadAsync())
            {
                var jobId = archiveReader.GetString(0);
                var status = archiveReader.GetString(1);
                var attempt = archiveReader.GetInt32(2);
                var runAfter = archiveReader.GetFieldValue<DateTimeOffset>(3);
                var lockedUntil = archiveReader.IsDBNull(4) ? (DateTimeOffset?)null : archiveReader.GetFieldValue<DateTimeOffset>(4);
                jobs.Add((jobId, status, attempt, runAfter, lockedUntil));
            }
            PrintJobTable("archive_jobs", jobs);
        }

        await using (var deliveryJobsCmd = new NpgsqlCommand(
                         """
                         SELECT job_id, status_key, attempt_count, run_after, locked_until
                         FROM public.jobs
                         WHERE job_type_key = 'project.delivery'
                           AND entity_type_key = 'project'
                           AND entity_key = @project_id
                         ORDER BY created_at DESC;
                         """,
                         conn
                     ))
        {
            deliveryJobsCmd.Parameters.AddWithValue("project_id", projectId);

            await using var deliveryReader = await deliveryJobsCmd.ExecuteReaderAsync();
            var jobs = new List<(string JobId, string Status, int AttemptCount, DateTimeOffset RunAfter, DateTimeOffset? LockedUntil)>();
            while (await deliveryReader.ReadAsync())
            {
                var jobId = deliveryReader.GetString(0);
                var status = deliveryReader.GetString(1);
                var attempt = deliveryReader.GetInt32(2);
                var runAfter = deliveryReader.GetFieldValue<DateTimeOffset>(3);
                var lockedUntil = deliveryReader.IsDBNull(4) ? (DateTimeOffset?)null : deliveryReader.GetFieldValue<DateTimeOffset>(4);
                jobs.Add((jobId, status, attempt, runAfter, lockedUntil));
            }
            PrintJobTable("delivery_jobs", jobs);
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: show failed: {ex.Message}");
        return 1;
    }
}

static void PrintLastEmailSummary(string metadataJson)
{
    try
    {
        using var doc = JsonDocument.Parse(metadataJson);
        if (!doc.RootElement.TryGetProperty("delivery", out var delivery)
            || !delivery.TryGetProperty("current", out var current)
            || !current.TryGetProperty("lastEmail", out var lastEmail)
            || lastEmail.ValueKind != JsonValueKind.Object)
        {
            Console.WriteLine("last_email: (none)");
            return;
        }

        var status = TryGetString(lastEmail, "status") ?? "(unknown)";
        var provider = TryGetString(lastEmail, "provider") ?? "(unknown)";
        var from = TryGetString(lastEmail, "fromAddress") ?? "(unknown)";
        var sentAt = TryGetString(lastEmail, "sentAtUtc") ?? "-";
        var error = TryGetString(lastEmail, "error");
        var toList = TryGetStringArray(lastEmail, "to");
        var to = toList.Count == 0 ? "-" : string.Join(",", toList);

        Console.WriteLine($"last_email: status={status} provider={provider} from={from} to={to} sentAtUtc={sentAt} error={(string.IsNullOrWhiteSpace(error) ? "-" : error)}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"last_email: (unreadable) {ex.Message}");
    }
}

static string? TryGetString(JsonElement element, string name)
{
    if (!element.TryGetProperty(name, out var prop))
    {
        return null;
    }

    return prop.ValueKind switch
    {
        JsonValueKind.String => prop.GetString(),
        JsonValueKind.Number => prop.GetRawText(),
        _ => null
    };
}

static DateTimeOffset? TryGetDateTimeOffset(JsonElement element, string name)
{
    var raw = TryGetString(element, name);
    if (string.IsNullOrWhiteSpace(raw))
    {
        return null;
    }

    return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
}

static IReadOnlyList<string> TryGetStringArray(JsonElement element, string name)
{
    if (!element.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Array)
    {
        return Array.Empty<string>();
    }

    return prop
        .EnumerateArray()
        .Where(value => value.ValueKind == JsonValueKind.String)
        .Select(value => value.GetString() ?? string.Empty)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToArray();
}

static void PrintJobTable(string title, IReadOnlyList<(string JobId, string Status, int AttemptCount, DateTimeOffset RunAfter, DateTimeOffset? LockedUntil)> jobs)
{
    var queuedCount = 0;
    var runningCount = 0;
    for (var i = 0; i < jobs.Count; i++)
    {
        var status = jobs[i].Status;
        if (string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase))
        {
            queuedCount++;
            continue;
        }

        if (string.Equals(status, "running", StringComparison.OrdinalIgnoreCase))
        {
            runningCount++;
        }
    }

    Console.WriteLine($"{title} (total={jobs.Count}, queued={queuedCount}, running={runningCount})");
    if (jobs.Count == 0)
    {
        Console.WriteLine("- (none)");
        return;
    }

    const int idWidth = 16;
    const int statusWidth = 12;
    const int attemptsWidth = 4;
    Console.WriteLine($"{Pad("JOB_ID", idWidth)} {Pad("STATUS", statusWidth)} {Pad("ATT", attemptsWidth)} {Pad("RUN_AFTER_LOCAL", 19)} {Pad("LOCKED_UNTIL_LOCAL", 19)} DUE");

    var now = DateTimeOffset.UtcNow;
    for (var i = 0; i < jobs.Count; i++)
    {
        var job = jobs[i];
        var jobId = Pad(ShortJobId(job.JobId), idWidth);
        var status = Pad(job.Status, statusWidth);
        var attempts = Pad(job.AttemptCount.ToString(), attemptsWidth);
        var runAfter = Pad(FormatLocalTime(job.RunAfter), 19);
        var lockedUntil = Pad(job.LockedUntil.HasValue ? FormatLocalTime(job.LockedUntil.Value) : "-", 19);
        var due = string.Equals(job.Status, "queued", StringComparison.OrdinalIgnoreCase)
            ? (job.RunAfter <= now ? "due" : "future")
            : "";

        Console.WriteLine($"{jobId} {status} {attempts} {runAfter} {lockedUntil} {due}");
    }
}

static string Pad(string value, int width)
{
    if (value.Length >= width)
    {
        return value;
    }

    return value.PadRight(width);
}

static string ShortJobId(string jobId)
{
    if (jobId.Length <= 12)
    {
        return jobId;
    }

    return $"{jobId.Substring(0, 8)}...{jobId.Substring(jobId.Length - 4, 4)}";
}

static string FormatLocalTime(DateTimeOffset value)
{
    return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}

static async Task<ExistingJob?> FindExistingDeliveryJobAsync(NpgsqlConnection conn, string projectId)
{
    await using var cmd = new NpgsqlCommand(
        """
        SELECT job_id, status_key
        FROM public.jobs
        WHERE job_type_key = 'project.delivery'
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

static async Task<ExistingJob?> FindExistingDeliveryEmailJobAsync(NpgsqlConnection conn, string projectId)
{
    await using var cmd = new NpgsqlCommand(
        """
        SELECT job_id, status_key
        FROM public.jobs
        WHERE job_type_key = 'project.delivery_email'
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

        var connectionString = DatabaseConnection.ResolveConnectionString(config);
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var contract = await LoadRootContractAsync(conn, providerKey, rootKey: "root");
        if (contract is null)
        {
            Console.Error.WriteLine($"devtest-clean: no storage_root_contracts row for provider={providerKey} root_key=root");
            return 1;
        }

        var effectiveContract = ApplyDevTestOverrides(providerKey, contract!);

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

static async Task<DevTestRootContract?> LoadRootContractAsync(NpgsqlConnection conn, string providerKey, string rootKey)
{
    await using var cmd = new NpgsqlCommand(
        """
        SELECT required_folders,
               optional_folders,
               allowed_extras,
               allowed_root_files,
               quarantine_relpath
        FROM storage_root_contracts
        WHERE provider_key = @provider
          AND root_key = @root_key
          AND is_active
        LIMIT 1;
        """,
        conn
    );
    cmd.Parameters.AddWithValue("provider", providerKey);
    cmd.Parameters.AddWithValue("root_key", rootKey);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return null;
    }

    var required = ParseJsonArray(reader.GetString(0));
    var optional = ParseJsonArray(reader.GetString(1));
    var allowedExtras = ParseJsonArray(reader.GetString(2));
    var allowedRootFiles = ParseJsonArray(reader.GetString(3));
    var quarantineRelpath = reader.IsDBNull(4) ? null : reader.GetString(4);

    return new DevTestRootContract(required, optional, allowedExtras, allowedRootFiles, quarantineRelpath);
}

static IReadOnlyList<string> ParseJsonArray(string json)
{
    if (string.IsNullOrWhiteSpace(json))
    {
        return Array.Empty<string>();
    }

    using var doc = JsonDocument.Parse(json);
    if (doc.RootElement.ValueKind != JsonValueKind.Array)
    {
        return Array.Empty<string>();
    }

    var list = new List<string>();
    foreach (var item in doc.RootElement.EnumerateArray())
    {
        if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
        {
            list.Add(item.GetString()!);
        }
    }

    return list;
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
            ["created_by"] = "MGF.ProjectBootstrapCli",
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

static async Task<int> RenderDeliveryPreviewAsync(
    string projectId,
    string outDir,
    IReadOnlyList<string> toEmails)
{
    try
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var project = await LoadProjectPreviewAsync(conn, projectId);
        if (project is null)
        {
            Console.Error.WriteLine($"bootstrap: email-preview project not found: {projectId}");
            return 1;
        }

        var clientName = await LoadClientNameAsync(conn, project.ClientId);
        var current = ReadDeliveryCurrentState(project.MetadataJson);
        if (string.IsNullOrWhiteSpace(current.ShareUrl))
        {
            Console.Error.WriteLine("bootstrap: email-preview stableShareUrl missing; run delivery first.");
            return 1;
        }

        var context = await TryBuildPreviewContextAsync(current, project.MetadataJson);
        if (context is null)
        {
            Console.Error.WriteLine("bootstrap: email-preview unable to build context from manifest or metadata.");
            return 1;
        }

        var tokens = ProvisioningTokens.Create(project.ProjectCode, project.ProjectName, clientName, Array.Empty<string>());
        var profile = EmailProfileResolver.Resolve(config, EmailProfiles.Deliveries);
        var replyTo = profile.DefaultReplyTo ?? "info@mgfilms.pro";
        var fromName = profile.DefaultFromName ?? "MG Films";

        var emailContext = new DeliveryReadyEmailContext(
            tokens,
            current.ShareUrl,
            context.VersionLabel,
            context.RetentionUntilUtc,
            context.Files,
            toEmails,
            replyTo,
            profile.LogoUrl,
            fromName);

        var composer = EmailComposerRegistry.CreateDefault().Get(EmailKind.DeliveryReady);
        var message = composer.Build(emailContext);

        Directory.CreateDirectory(outDir);
        var previewTextPath = Path.Combine(outDir, "preview.txt");
        var previewHtmlPath = Path.Combine(outDir, "preview.html");
        var previewJsonPath = Path.Combine(outDir, "preview.json");

        await File.WriteAllTextAsync(previewTextPath, message.BodyText);
        await File.WriteAllTextAsync(previewHtmlPath, message.HtmlBody ?? message.BodyText);

        var preview = new
        {
            kind = "delivery_ready",
            projectId = project.ProjectId,
            projectCode = project.ProjectCode,
            projectName = project.ProjectName,
            clientName,
            subject = message.Subject,
            stablePath = current.StablePath,
            apiStablePath = current.ApiStablePath,
            stableShareUrl = current.ShareUrl,
            currentVersion = context.VersionLabel,
            retentionUntilUtc = context.RetentionUntilUtc,
            recipients = toEmails,
            replyTo,
            fromName,
            logoUrl = profile.LogoUrl,
            files = context.Files.Select(file => new
            {
                relativePath = file.RelativePath,
                sizeBytes = file.SizeBytes,
                lastWriteTimeUtc = file.LastWriteTimeUtc
            })
        };

        var json = JsonSerializer.Serialize(preview, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(previewJsonPath, json);

        Console.WriteLine($"bootstrap: email-preview wrote {previewTextPath}");
        Console.WriteLine($"bootstrap: email-preview wrote {previewHtmlPath}");
        Console.WriteLine($"bootstrap: email-preview wrote {previewJsonPath}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: email-preview failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> RenderDeliveryPreviewFromFixtureAsync(
    string fixture,
    string outDir,
    IReadOnlyList<string> toEmails,
    bool writeSnapshots,
    string? snapshotOut)
{
    try
    {
        var templatesRoot = ResolveEmailTemplatesRoot();
        var fixturePath = ResolveFixturePath(templatesRoot, fixture);
        if (!File.Exists(fixturePath))
        {
            Console.Error.WriteLine($"bootstrap: email-preview fixture not found: {fixturePath}");
            return 1;
        }

        var json = await File.ReadAllTextAsync(fixturePath);
        var fixtureModel = JsonSerializer.Deserialize<DeliveryPreviewFixture>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (fixtureModel is null)
        {
            Console.Error.WriteLine($"bootstrap: email-preview invalid fixture: {fixturePath}");
            return 1;
        }

        var tokens = ProvisioningTokens.Create(
            fixtureModel.ProjectCode ?? "MGF25-TEST",
            fixtureModel.ProjectName ?? "Delivery Preview",
            fixtureModel.ClientName ?? "Client",
            Array.Empty<string>());

        var files = fixtureModel.Files?.Select(file =>
            new DeliveryFileSummary(
                file.RelativePath ?? "deliverable.mp4",
                file.SizeBytes ?? 0,
                file.LastWriteTimeUtc ?? DateTimeOffset.UtcNow))
            .ToArray() ?? Array.Empty<DeliveryFileSummary>();

        var config = BuildConfiguration();
        var profile = EmailProfileResolver.Resolve(config, EmailProfiles.Deliveries);
        var replyTo = profile.DefaultReplyTo ?? "info@mgfilms.pro";
        var fromName = profile.DefaultFromName ?? "MG Films";

        var recipients = toEmails.Count > 0
            ? toEmails
            : (fixtureModel.Recipients?.Where(email => !string.IsNullOrWhiteSpace(email)).ToArray() ?? Array.Empty<string>());
        if (recipients.Count == 0)
        {
            recipients = new[] { "info@mgfilms.pro" };
        }

        var logoUrl = string.IsNullOrWhiteSpace(fixtureModel.LogoUrl) ? null : fixtureModel.LogoUrl;

        var emailContext = new DeliveryReadyEmailContext(
            tokens,
            fixtureModel.ShareUrl ?? "https://dropbox.test/final",
            fixtureModel.VersionLabel ?? "v1",
            fixtureModel.RetentionUntilUtc ?? DateTimeOffset.UtcNow.AddMonths(3),
            files,
            recipients,
            replyTo,
            logoUrl,
            fromName);

        var composer = EmailComposerRegistry.CreateDefault().Get(EmailKind.DeliveryReady);
        var message = composer.Build(emailContext);

        Directory.CreateDirectory(outDir);
        var previewTextPath = Path.Combine(outDir, "preview.txt");
        var previewHtmlPath = Path.Combine(outDir, "preview.html");
        var previewJsonPath = Path.Combine(outDir, "preview.json");

        await File.WriteAllTextAsync(previewTextPath, message.BodyText);
        await File.WriteAllTextAsync(previewHtmlPath, message.HtmlBody ?? message.BodyText);

        var preview = new
        {
            kind = "delivery_ready",
            fixture = fixture,
            projectCode = fixtureModel.ProjectCode,
            projectName = fixtureModel.ProjectName,
            clientName = fixtureModel.ClientName,
            subject = message.Subject,
            stableShareUrl = fixtureModel.ShareUrl,
            currentVersion = fixtureModel.VersionLabel,
            retentionUntilUtc = fixtureModel.RetentionUntilUtc,
            recipients,
            replyTo,
            fromName,
            logoUrl,
            files = files.Select(file => new
            {
                relativePath = file.RelativePath,
                sizeBytes = file.SizeBytes,
                lastWriteTimeUtc = file.LastWriteTimeUtc
            })
        };

        var previewJson = JsonSerializer.Serialize(preview, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(previewJsonPath, previewJson);

        Console.WriteLine($"bootstrap: email-preview wrote {previewTextPath}");
        Console.WriteLine($"bootstrap: email-preview wrote {previewHtmlPath}");
        Console.WriteLine($"bootstrap: email-preview wrote {previewJsonPath}");

        if (writeSnapshots)
        {
            await WriteSnapshotAsync(fixture, message.HtmlBody ?? message.BodyText, snapshotOut);
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"bootstrap: email-preview failed: {ex.Message}");
        return 1;
    }
}

static string ResolveEmailTemplatesRoot()
{
    var baseDir = AppContext.BaseDirectory;
    var runtimePath = Path.Combine(baseDir, "Email", "Templates");
    if (Directory.Exists(runtimePath) && HasEmailTemplates(runtimePath))
    {
        return runtimePath;
    }

    throw new DirectoryNotFoundException($"Email templates folder not found at {runtimePath}.");
}

static bool HasEmailTemplates(string templatesRoot)
{
    return Directory.EnumerateFiles(templatesRoot, "*.html", SearchOption.AllDirectories).Any()
        || Directory.EnumerateFiles(templatesRoot, "*.txt", SearchOption.AllDirectories).Any();
}

static string ResolveFixturePath(string templatesRoot, string fixture)
{
    var normalized = fixture.Trim();
    if (normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || normalized.Contains(Path.DirectorySeparatorChar))
    {
        return Path.GetFullPath(normalized);
    }

    return Path.Combine(templatesRoot, "fixtures", $"{normalized}.json");
}

static async Task WriteSnapshotAsync(string fixture, string htmlBody, string? snapshotOut)
{
    if (string.IsNullOrWhiteSpace(snapshotOut))
    {
        Console.Error.WriteLine("bootstrap: email-preview snapshot output path is required.");
        return;
    }

    var snapshotsDir = Path.GetFullPath(snapshotOut);
    Directory.CreateDirectory(snapshotsDir);
    var snapshotPath = Path.Combine(snapshotsDir, $"delivery_ready_{fixture}.html");
    await File.WriteAllTextAsync(snapshotPath, htmlBody);
    Console.WriteLine($"bootstrap: email-preview wrote snapshot {snapshotPath}");
}

static async Task<DeliveryPreviewProject?> LoadProjectPreviewAsync(NpgsqlConnection conn, string projectId)
{
    await using var cmd = new NpgsqlCommand(
        """
        SELECT project_id, project_code, name, client_id, metadata
        FROM public.projects
        WHERE project_id = @project_id
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

    return new DeliveryPreviewProject(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4));
}

static async Task<string?> LoadClientNameAsync(NpgsqlConnection conn, string clientId)
{
    await using var cmd = new NpgsqlCommand(
        """
        SELECT display_name
        FROM public.clients
        WHERE client_id = @client_id
        LIMIT 1;
        """,
        conn
    );

    cmd.Parameters.AddWithValue("client_id", clientId);

    var result = await cmd.ExecuteScalarAsync();
    return result is DBNull ? null : result?.ToString();
}

static DeliveryPreviewCurrent ReadDeliveryCurrentState(string metadataJson)
{
    try
    {
        using var doc = JsonDocument.Parse(metadataJson);
        if (!doc.RootElement.TryGetProperty("delivery", out var delivery))
        {
            return new DeliveryPreviewCurrent(null, null, null, null, null);
        }

        if (!delivery.TryGetProperty("current", out var current))
        {
            return new DeliveryPreviewCurrent(null, null, null, null, null);
        }

        return new DeliveryPreviewCurrent(
            StablePath: TryGetString(current, "stablePath"),
            ApiStablePath: TryGetString(current, "apiStablePath"),
            ShareUrl: TryGetString(current, "stableShareUrl") ?? TryGetString(current, "shareUrl"),
            CurrentVersion: TryGetString(current, "currentVersion"),
            RetentionUntilUtc: TryGetDateTimeOffset(current, "retentionUntilUtc")
        );
    }
    catch
    {
        return new DeliveryPreviewCurrent(null, null, null, null, null);
    }
}

static async Task<DeliveryPreviewContext?> TryBuildPreviewContextAsync(
    DeliveryPreviewCurrent current,
    string metadataJson)
{
    if (!string.IsNullOrWhiteSpace(current.ApiStablePath))
    {
        return TryBuildContextFromMetadata(metadataJson);
    }

    if (!string.IsNullOrWhiteSpace(current.StablePath))
    {
        if (TryResolveContainerRoot(current.StablePath, out var containerRoot, out _))
        {
            var manifestPath = Path.Combine(containerRoot, "00_Admin", ".mgf", "manifest", "delivery_manifest.json");
            var manifest = await TryReadManifestContextAsync(manifestPath);
            if (manifest is not null)
            {
                return manifest;
            }
        }
    }

    return TryBuildContextFromMetadata(metadataJson);
}

static async Task<DeliveryPreviewContext?> TryReadManifestContextAsync(string manifestPath)
{
    if (!File.Exists(manifestPath))
    {
        return null;
    }

    try
    {
        var json = await File.ReadAllTextAsync(manifestPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var files = root.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array
            ? ReadDeliveryFiles(filesElement)
            : new List<DeliveryFileSummary>();

        var versionLabel = TryGetString(root, "currentVersion") ?? TryGetString(root, "versionLabel") ?? "v1";
        var retentionUntil = TryGetDateTimeOffset(root, "retentionUntilUtc") ?? DateTimeOffset.UtcNow.AddMonths(3);

        if (files.Count == 0)
        {
            return null;
        }

        return new DeliveryPreviewContext(files, versionLabel, retentionUntil);
    }
    catch
    {
        return null;
    }
}

static DeliveryPreviewContext? TryBuildContextFromMetadata(string metadataJson)
{
    try
    {
        using var doc = JsonDocument.Parse(metadataJson);
        if (!doc.RootElement.TryGetProperty("delivery", out var delivery))
        {
            return null;
        }

        var currentVersion = string.Empty;
        var retentionUntil = DateTimeOffset.UtcNow.AddMonths(3);
        if (delivery.TryGetProperty("current", out var current))
        {
            currentVersion = TryGetString(current, "currentVersion") ?? string.Empty;
            retentionUntil = TryGetDateTimeOffset(current, "retentionUntilUtc") ?? retentionUntil;
        }

        if (!delivery.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        for (var index = runs.GetArrayLength() - 1; index >= 0; index--)
        {
            var run = runs[index];
            if (run.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var versionLabel = TryGetString(run, "versionLabel") ?? currentVersion;
            if (string.IsNullOrWhiteSpace(versionLabel))
            {
                versionLabel = "v1";
            }

            var runRetention = TryGetDateTimeOffset(run, "retentionUntilUtc") ?? retentionUntil;

            if (run.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
            {
                var files = ReadDeliveryFiles(filesElement);
                if (files.Count > 0)
                {
                    return new DeliveryPreviewContext(files, versionLabel, runRetention);
                }
            }
        }

        return null;
    }
    catch
    {
        return null;
    }
}

static bool TryResolveContainerRoot(string stablePath, out string containerRoot, out string error)
{
    containerRoot = string.Empty;
    error = string.Empty;

    var trimmed = stablePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var finalSegment = Path.GetFileName(trimmed);
    if (!string.Equals(finalSegment, "Final", StringComparison.OrdinalIgnoreCase))
    {
        error = $"Stable path does not point to Final folder: {stablePath}";
        return false;
    }

    var deliverablesDir = Path.GetDirectoryName(trimmed);
    if (string.IsNullOrWhiteSpace(deliverablesDir))
    {
        error = "Delivery path missing deliverables parent.";
        return false;
    }

    var deliverablesSegment = Path.GetFileName(deliverablesDir);
    if (!string.Equals(deliverablesSegment, "01_Deliverables", StringComparison.OrdinalIgnoreCase))
    {
        error = "Delivery path missing 01_Deliverables segment.";
        return false;
    }

    var container = Path.GetDirectoryName(deliverablesDir);
    if (string.IsNullOrWhiteSpace(container))
    {
        error = "Delivery container root could not be resolved.";
        return false;
    }

    containerRoot = container;
    return true;
}

static List<DeliveryFileSummary> ReadDeliveryFiles(JsonElement filesElement)
{
    var list = new List<DeliveryFileSummary>();
    foreach (var element in filesElement.EnumerateArray())
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            continue;
        }

        var relative = TryGetString(element, "relativePath");
        if (string.IsNullOrWhiteSpace(relative))
        {
            continue;
        }

        var sizeRaw = TryGetString(element, "sizeBytes");
        if (!long.TryParse(sizeRaw, out var sizeBytes))
        {
            sizeBytes = 0;
        }

        var lastWrite = TryGetDateTimeOffset(element, "lastWriteTimeUtc") ?? DateTimeOffset.MinValue;
        list.Add(new DeliveryFileSummary(relative, sizeBytes, lastWrite));
    }

    return list;
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

static IReadOnlyList<string> ParseEmails(IEnumerable<string> emails)
{
    return emails
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

sealed record ProjectDeliveryJobPayload(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    IReadOnlyList<string> ToEmails,
    string? ReplyToEmail,
    bool TestMode,
    bool AllowTestCleanup,
    bool AllowNonReal,
    bool Force,
    bool RefreshShareLink
);

sealed record ProjectDeliveryEmailJobPayload(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    IReadOnlyList<string> ToEmails,
    string? ReplyToEmail
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

sealed record DeliveryPreviewProject(
    string ProjectId,
    string ProjectCode,
    string ProjectName,
    string ClientId,
    string MetadataJson);

sealed record DeliveryPreviewContext(
    IReadOnlyList<DeliveryFileSummary> Files,
    string VersionLabel,
    DateTimeOffset RetentionUntilUtc);

sealed record DeliveryPreviewCurrent(
    string? StablePath,
    string? ApiStablePath,
    string? ShareUrl,
    string? CurrentVersion,
    DateTimeOffset? RetentionUntilUtc);

sealed record DeliveryPreviewFixture(
    string? ProjectCode,
    string? ProjectName,
    string? ClientName,
    string? ShareUrl,
    string? VersionLabel,
    DateTimeOffset? RetentionUntilUtc,
    DeliveryPreviewFixtureFile[]? Files,
    string[]? Recipients,
    string? LogoUrl);

sealed record DeliveryPreviewFixtureFile(
    string? RelativePath,
    long? SizeBytes,
    DateTimeOffset? LastWriteTimeUtc);
