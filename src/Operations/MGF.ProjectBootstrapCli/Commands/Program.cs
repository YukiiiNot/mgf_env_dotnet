using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MGF.FolderProvisioning;
using MGF.Operations.Runtime;
using MGF.Contracts.Abstractions.Email;
using MGF.Email.Models;
using MGF.Email.Registry;
using MGF.UseCases.DeliveryEmail.RenderDeliveryEmailPreview;
using MGF.UseCases.Operations.Jobs.EnqueueProjectArchiveJob;
using MGF.UseCases.Operations.Jobs.EnqueueProjectBootstrapJob;
using MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryEmailJob;
using MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryJob;
using MGF.UseCases.Operations.Jobs.EnqueueRootIntegrityJob;
using MGF.UseCases.Operations.Jobs.GetRootIntegrityJobs;
using MGF.UseCases.Operations.Jobs.RequeueStaleJobs;
using MGF.UseCases.Operations.Jobs.ResetProjectJobs;
using MGF.UseCases.Operations.Projects.GetDeliveryEmailPreviewData;
using MGF.UseCases.Operations.Projects.GetProjectSnapshot;
using MGF.UseCases.Operations.Projects.ListProjects;
using MGF.UseCases.Operations.Projects.UpdateProjectStatus;

var root = new RootCommand("MGF Project Bootstrap Job Tool");
root.AddCommand(CreateEnqueueCommand());
root.AddCommand(CreateReadyCommand());
root.AddCommand(CreateToArchiveCommand());
root.AddCommand(CreateArchiveCommand());
root.AddCommand(CreateToDeliverCommand());
root.AddCommand(CreateDeliverCommand());
root.AddCommand(CreateDeliveryEmailCommand());
root.AddCommand(CreateEmailPreviewCommand());
root.AddCommand(CreateRootAuditCommand());
root.AddCommand(CreateRootRepairCommand());
root.AddCommand(CreateRootShowCommand());
root.AddCommand(CreateShowCommand());
root.AddCommand(CreateListCommand());
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
        var runtime = CreateRuntime();
        var result = await runtime.EnqueueProjectBootstrapJob.ExecuteAsync(new EnqueueProjectBootstrapJobRequest(
            payload.ProjectId,
            payload.EditorInitials,
            payload.VerifyDomainRoots,
            payload.CreateDomainRoots,
            payload.ProvisionProjectContainers,
            payload.AllowRepair,
            payload.ForceSandbox,
            payload.AllowNonReal,
            payload.Force,
            payload.TestMode,
            payload.AllowTestCleanup));

        if (!result.Enqueued)
        {
            Console.WriteLine($"bootstrap: {result.Reason} for project_id={payload.ProjectId}");
            return 0;
        }

        Console.WriteLine($"bootstrap: enqueued job_id={result.JobId} project_id={payload.ProjectId}");
        Console.WriteLine($"bootstrap: payload={result.PayloadJson}");
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
        var runtime = CreateRuntime();
        var result = await runtime.EnqueueProjectArchiveJob.ExecuteAsync(new EnqueueProjectArchiveJobRequest(
            payload.ProjectId,
            payload.EditorInitials,
            payload.TestMode,
            payload.AllowTestCleanup,
            payload.AllowNonReal,
            payload.Force));

        if (!result.Enqueued)
        {
            Console.WriteLine($"archive: {result.Reason} for project_id={payload.ProjectId}");
            return 0;
        }

        Console.WriteLine($"archive: enqueued job_id={result.JobId} project_id={payload.ProjectId}");
        Console.WriteLine($"archive: payload={result.PayloadJson}");
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
        var runtime = CreateRuntime();
        var result = await runtime.EnqueueProjectDeliveryJob.ExecuteAsync(new EnqueueProjectDeliveryJobRequest(
            payload.ProjectId,
            payload.EditorInitials,
            payload.ToEmails,
            payload.ReplyToEmail,
            payload.TestMode,
            payload.AllowTestCleanup,
            payload.AllowNonReal,
            payload.Force,
            payload.RefreshShareLink));

        if (!result.Enqueued)
        {
            Console.Error.WriteLine($"bootstrap: delivery enqueue blocked: {result.Reason}");
            return 2;
        }

        Console.WriteLine($"bootstrap: project.delivery enqueued job_id={result.JobId} project_id={payload.ProjectId}");
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
        var runtime = CreateRuntime();
        var result = await runtime.EnqueueProjectDeliveryEmailJob.ExecuteAsync(new EnqueueProjectDeliveryEmailJobRequest(
            payload.ProjectId,
            payload.EditorInitials,
            payload.ToEmails,
            payload.ReplyToEmail));

        if (!result.Enqueued)
        {
            Console.Error.WriteLine($"bootstrap: delivery-email enqueue blocked: {result.Reason}");
            return 2;
        }

        Console.WriteLine($"bootstrap: project.delivery_email enqueued job_id={result.JobId} project_id={payload.ProjectId}");
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
        var runtime = CreateRuntime();
        var result = await runtime.EnqueueRootIntegrityJob.ExecuteAsync(new EnqueueRootIntegrityJobRequest(
            payload.ProviderKey,
            payload.RootKey,
            payload.Mode,
            payload.DryRun,
            payload.QuarantineRelpath,
            payload.MaxItems,
            payload.MaxBytes));

        Console.WriteLine($"root-integrity: enqueued job_id={result.JobId} provider={payload.ProviderKey} root_key={payload.RootKey}");
        Console.WriteLine($"root-integrity: payload={result.PayloadJson}");
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
        var runtime = CreateRuntime();
        var result = await runtime.UpdateProjectStatus.ExecuteAsync(new UpdateProjectStatusRequest(
            projectId,
            "ready_to_provision"));

        var rows = result.RowsAffected;
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
        var runtime = CreateRuntime();
        var result = await runtime.UpdateProjectStatus.ExecuteAsync(new UpdateProjectStatusRequest(
            projectId,
            "to_archive"));

        var rows = result.RowsAffected;
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

static async Task<int> MarkToDeliverAsync(string projectId)
{
    try
    {
        var runtime = CreateRuntime();
        var result = await runtime.UpdateProjectStatus.ExecuteAsync(new UpdateProjectStatusRequest(
            projectId,
            "ready_to_deliver"));

        var rows = result.RowsAffected;
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

static async Task<int> ShowProjectAsync(string projectId, bool jobsOnly)
{
    try
    {
        var runtime = CreateRuntime();
        var snapshot = await runtime.GetProjectSnapshot.ExecuteAsync(new GetProjectSnapshotRequest(
            projectId,
            IncludeStorageRoots: !jobsOnly));

        if (snapshot is null)
        {
            Console.Error.WriteLine($"bootstrap: project not found: {projectId}");
            return 1;
        }

        var project = snapshot.Project;
        if (!jobsOnly)
        {
            Console.WriteLine($"project_id={project.ProjectId}");
            Console.WriteLine($"project_code={project.ProjectCode}");
            Console.WriteLine($"name={project.ProjectName}");
            Console.WriteLine($"status_key={project.StatusKey}");
            Console.WriteLine($"data_profile={project.DataProfile}");

            var pretty = JsonSerializer.Serialize(
                JsonDocument.Parse(project.MetadataJson).RootElement,
                new JsonSerializerOptions { WriteIndented = true }
            );
            Console.WriteLine("metadata:");
            Console.WriteLine(pretty);
        }
        else
        {
            PrintLastEmailSummary(project.MetadataJson);
        }

        if (!jobsOnly)
        {
            Console.WriteLine("storage_roots:");
            if (snapshot.StorageRoots.Count == 0)
            {
                Console.WriteLine("- (none)");
            }
            else
            {
                foreach (var root in snapshot.StorageRoots)
                {
                    Console.WriteLine($"- {root.ProjectStorageRootId}\t{root.StorageProviderKey}\t{root.RootKey}\t{root.FolderRelpath}\tis_primary={root.IsPrimary}\tcreated_at={root.CreatedAtUtc:O}");
                }
            }
        }

        var bootstrapJobs = snapshot.BootstrapJobs
            .Select(job => (job.JobId, job.StatusKey, job.AttemptCount, job.RunAfter, job.LockedUntil))
            .ToList();
        PrintJobTable("bootstrap_jobs", bootstrapJobs);

        var archiveJobs = snapshot.ArchiveJobs
            .Select(job => (job.JobId, job.StatusKey, job.AttemptCount, job.RunAfter, job.LockedUntil))
            .ToList();
        PrintJobTable("archive_jobs", archiveJobs);

        var deliveryJobs = snapshot.DeliveryJobs
            .Select(job => (job.JobId, job.StatusKey, job.AttemptCount, job.RunAfter, job.LockedUntil))
            .ToList();
        PrintJobTable("delivery_jobs", deliveryJobs);
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

static async Task<int> ShowRootIntegrityAsync(string provider, string rootKey)
{
    try
    {
        Console.WriteLine($"root_integrity_jobs ({provider}:{rootKey}):");

        var hadRows = false;
        var runtime = CreateRuntime();
        var result = await runtime.GetRootIntegrityJobs.ExecuteAsync(new GetRootIntegrityJobsRequest(
            provider,
            rootKey,
            Limit: 5));

        foreach (var job in result.Jobs)
        {
            hadRows = true;
            var summary = SummarizeRootIntegrityPayload(job.PayloadJson);
            Console.WriteLine($"- {job.JobId}\t{job.StatusKey}\tattempts={job.AttemptCount}\trun_after={job.RunAfter:O}\tlocked_until={(job.LockedUntil.HasValue ? job.LockedUntil.Value.ToString("O") : "(null)")} {summary}");
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
        var runtime = CreateRuntime();
        var result = await runtime.ListProjects.ExecuteAsync(new ListProjectsRequest(limit));

        foreach (var project in result.Projects)
        {
            Console.WriteLine($"{project.ProjectId}\t{project.ProjectCode}\t{project.ProjectName}");
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

static async Task<int> ResetJobsAsync(string projectId, string jobTypeKey)
{
    try
    {
        var runtime = CreateRuntime();
        var result = await runtime.ResetProjectJobs.ExecuteAsync(new ResetProjectJobsRequest(
            projectId,
            jobTypeKey));

        Console.WriteLine($"bootstrap: reset {result.RowsAffected} job(s) for project_id={projectId} (job_type_key={jobTypeKey})");
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
        var runtime = CreateRuntime();
        var result = await runtime.RequeueStaleJobs.ExecuteAsync(new RequeueStaleJobsRequest(dryRun));
        if (result.WasDryRun)
        {
            Console.WriteLine($"bootstrap: stale running jobs (dry-run) count={result.Count}");
            return 0;
        }

        Console.WriteLine($"bootstrap: requeued stale running jobs count={result.Count}");
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
        var runtime = OperationsRuntime.Create(config);
        var previewData = await runtime.GetDeliveryEmailPreviewData.ExecuteAsync(
            new GetDeliveryEmailPreviewDataRequest(projectId));

        if (previewData is null)
        {
            Console.Error.WriteLine($"bootstrap: email-preview project not found: {projectId}");
            return 1;
        }

        var project = previewData.Project;
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

        var tokens = ProvisioningTokens.Create(project.ProjectCode, project.ProjectName, project.ClientName, Array.Empty<string>());
        var profile = EmailProfileResolver.Resolve(config, EmailProfiles.Deliveries);
        var replyTo = profile.DefaultReplyTo ?? "info@mgfilms.pro";
        var fromName = profile.DefaultFromName ?? "MG Films";

        var useCase = new RenderDeliveryEmailPreviewUseCase();
        var previewResult = await useCase.ExecuteAsync(new RenderDeliveryEmailPreviewRequest(
            tokens,
            current.ShareUrl,
            context.VersionLabel,
            context.RetentionUntilUtc,
            context.Files,
            toEmails,
            replyTo,
            profile.LogoUrl,
            fromName));
        var message = previewResult.Message;

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
            clientName = project.ClientName,
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
            new DeliveryEmailFileSummary(
                file.RelativePath ?? "deliverable.mp4",
                file.SizeBytes ?? 0,
                file.LastWriteTimeUtc ?? DateTimeOffset.UtcNow))
            .ToArray() ?? Array.Empty<DeliveryEmailFileSummary>();

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

        var useCase = new RenderDeliveryEmailPreviewUseCase();
        var previewResult = await useCase.ExecuteAsync(new RenderDeliveryEmailPreviewRequest(
            tokens,
            fixtureModel.ShareUrl ?? "https://dropbox.test/final",
            fixtureModel.VersionLabel ?? "v1",
            fixtureModel.RetentionUntilUtc ?? DateTimeOffset.UtcNow.AddMonths(3),
            files,
            recipients,
            replyTo,
            logoUrl,
            fromName));
        var message = previewResult.Message;

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
            : new List<DeliveryEmailFileSummary>();

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

static List<DeliveryEmailFileSummary> ReadDeliveryFiles(JsonElement filesElement)
{
    var list = new List<DeliveryEmailFileSummary>();
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
        list.Add(new DeliveryEmailFileSummary(relative, sizeBytes, lastWrite));
    }

    return list;
}

static IConfiguration BuildConfiguration()
{
    return OperationsRuntimeConfiguration.BuildConfiguration();
}

static OperationsRuntime CreateRuntime()
{
    var config = BuildConfiguration();
    return OperationsRuntime.Create(config);
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


sealed record DeliveryPreviewContext(
    IReadOnlyList<DeliveryEmailFileSummary> Files,
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




