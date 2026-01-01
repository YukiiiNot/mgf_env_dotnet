namespace MGF.Worker;

using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MGF.Domain.Entities;
using MGF.Data.Abstractions;
using MGF.Data.Data;
using MGF.Data.Stores.Counters;
using MGF.Data.Stores.Delivery;
using MGF.Data.Stores.Jobs;
using MGF.Worker.Square;
using MGF.Worker.ProjectArchive;
using MGF.Worker.ProjectBootstrap;
using MGF.Worker.ProjectDelivery;
using MGF.Worker.RootIntegrity;
using MGF.UseCases.DeliveryEmail.SendDeliveryEmail;
using MGF.UseCases.ProjectBootstrap.BootstrapProject;

public sealed class JobWorker : BackgroundService
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(5);

    private const string SquareProcessorKey = "square";
    private const string SquarePaymentMethodKey = "square";
    private const string LedgerProjectName = "Square Transactions (Imported)";
    private const string SquareReviewTypeMissingClientMapping = "square.payment.missing_client_mapping";
    private const string SquareReviewTypeUnhandledWebhookEventType = "square.webhook.unhandled_event_type";

    private readonly IServiceScopeFactory scopeFactory;
    private readonly IConfiguration configuration;
    private readonly SquareApiClient squareApiClient;
    private readonly ILogger<JobWorker> logger;
    private readonly IHostApplicationLifetime appLifetime;
    private readonly string workerId;
    private readonly int? maxJobs;
    private readonly bool exitWhenIdle;
    private int processedJobs;

    public JobWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        SquareApiClient squareApiClient,
        ILogger<JobWorker> logger,
        IHostApplicationLifetime appLifetime
    )
    {
        this.scopeFactory = scopeFactory;
        this.configuration = configuration;
        this.squareApiClient = squareApiClient;
        this.logger = logger;
        this.appLifetime = appLifetime;
        workerId = $"mgf_worker_{Environment.MachineName}_{Guid.NewGuid():N}";
        maxJobs = ParseMaxJobs(configuration["Worker:MaxJobs"]);
        exitWhenIdle = string.Equals(configuration["Worker:ExitWhenIdle"], "true", StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MGF.Worker: starting (worker_id={WorkerId})", workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var jobQueueStore = scope.ServiceProvider.GetRequiredService<IJobQueueStore>();
                var counterAllocator = scope.ServiceProvider.GetRequiredService<ICounterAllocator>();
                var deliveryStore = scope.ServiceProvider.GetRequiredService<IProjectDeliveryStore>();

                var reaped = await ReapStaleRunningJobsAsync(jobQueueStore, stoppingToken);
                if (reaped > 0)
                {
                    logger.LogInformation("MGF.Worker: reaped {ReapedCount} stale running jobs", reaped);
                }

                var job = await TryClaimJobAsync(jobQueueStore, stoppingToken);
                if (job is null)
                {
                    if (exitWhenIdle)
                    {
                        logger.LogInformation("MGF.Worker: exiting (idle with exitWhenIdle=true)");
                        appLifetime.StopApplication();
                        break;
                    }

                    await Task.Delay(DefaultPollInterval, stoppingToken);
                    continue;
                }

                await RunJobAsync(
                    db,
                    jobQueueStore,
                    counterAllocator,
                    deliveryStore,
                    job,
                    scope.ServiceProvider,
                    stoppingToken);

                processedJobs++;
                if (maxJobs.HasValue && processedJobs >= maxJobs.Value)
                {
                    logger.LogInformation("MGF.Worker: exiting after processing {ProcessedJobs} jobs", processedJobs);
                    appLifetime.StopApplication();
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MGF.Worker: loop error");
                await Task.Delay(ErrorBackoff, stoppingToken);
            }
        }
    }

    private sealed record ClaimedJob(
        string JobId,
        string JobTypeKey,
        int AttemptCount,
        int MaxAttempts,
        string PayloadJson
    );

    private async Task<ClaimedJob?> TryClaimJobAsync(IJobQueueStore jobQueueStore, CancellationToken cancellationToken)
    {
        var claimed = await jobQueueStore.TryClaimJobAsync(workerId, LockDuration, cancellationToken);
        if (claimed is null)
        {
            return null;
        }

        logger.LogInformation(
            "MGF.Worker: claimed job {JobId} (type={JobTypeKey}, attempt={Attempt}/{Max})",
            claimed.JobId,
            claimed.JobTypeKey,
            claimed.AttemptCount + 1,
            claimed.MaxAttempts
        );

        return new ClaimedJob(
            claimed.JobId,
            claimed.JobTypeKey,
            claimed.AttemptCount,
            claimed.MaxAttempts,
            claimed.PayloadJson);
    }

    private static int? ParseMaxJobs(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return null;
    }

    private Task<int> ReapStaleRunningJobsAsync(
        IJobQueueStore jobQueueStore,
        CancellationToken cancellationToken)
    {
        return jobQueueStore.ReapStaleRunningJobsAsync(cancellationToken);
    }

    private async Task RunJobAsync(
        AppDbContext db,
        IJobQueueStore jobQueueStore,
        ICounterAllocator counterAllocator,
        IProjectDeliveryStore deliveryStore,
        ClaimedJob job,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.Equals(job.JobTypeKey, "dropbox.create_project_structure", StringComparison.Ordinal))
            {
                await HandleDropboxCreateProjectStructureAsync(job, cancellationToken);
                await MarkSucceededAsync(jobQueueStore, job.JobId, cancellationToken);
                return;
            }

            if (string.Equals(job.JobTypeKey, "square.webhook_event.process", StringComparison.Ordinal))
            {
                var webhookStore = services.GetRequiredService<ISquareWebhookStore>();
                var succeeded = await HandleSquareWebhookEventProcessAsync(
                    db,
                    jobQueueStore,
                    job,
                    webhookStore,
                    cancellationToken);
                if (succeeded)
                {
                    await MarkSucceededAsync(jobQueueStore, job.JobId, cancellationToken);
                }
                return;
            }

            if (string.Equals(job.JobTypeKey, "square.reconcile.payments", StringComparison.Ordinal))
            {
                await HandleSquareReconcilePaymentsAsync(db, job, cancellationToken);
                await MarkSucceededAsync(jobQueueStore, job.JobId, cancellationToken);
                return;
            }

            if (string.Equals(job.JobTypeKey, "square.payment.upsert", StringComparison.Ordinal))
            {
                await HandleSquarePaymentUpsertAsync(db, counterAllocator, job, cancellationToken);
                await MarkSucceededAsync(jobQueueStore, job.JobId, cancellationToken);
                return;
            }

            if (string.Equals(job.JobTypeKey, "project.bootstrap", StringComparison.Ordinal))
            {
                var useCase = services.GetRequiredService<IBootstrapProjectUseCase>();
                var succeeded = await HandleProjectBootstrapAsync(
                    jobQueueStore,
                    useCase,
                    job,
                    cancellationToken);
                if (succeeded)
                {
                    await MarkSucceededAsync(jobQueueStore, job.JobId, cancellationToken);
                }
                return;
            }

            if (string.Equals(job.JobTypeKey, "project.archive", StringComparison.Ordinal))
            {
                var succeeded = await HandleProjectArchiveAsync(db, jobQueueStore, job, cancellationToken);
                if (succeeded)
                {
                    await MarkSucceededAsync(jobQueueStore, job.JobId, cancellationToken);
                }
                return;
            }

            if (string.Equals(job.JobTypeKey, "project.delivery", StringComparison.Ordinal))
            {
                var succeeded = await HandleProjectDeliveryAsync(db, jobQueueStore, deliveryStore, job, cancellationToken);
                if (succeeded)
                {
                    await MarkSucceededAsync(jobQueueStore, job.JobId, cancellationToken);
                }
                return;
            }

            if (string.Equals(job.JobTypeKey, "project.delivery_email", StringComparison.Ordinal))
            {
                var useCase = services.GetRequiredService<ISendDeliveryEmailUseCase>();
                var succeeded = await HandleProjectDeliveryEmailAsync(
                    db,
                    jobQueueStore,
                    deliveryStore,
                    job,
                    useCase,
                    cancellationToken);
                if (succeeded)
                {
                    await MarkSucceededAsync(jobQueueStore, job.JobId, cancellationToken);
                }
                return;
            }

            if (string.Equals(job.JobTypeKey, "domain.root_integrity", StringComparison.Ordinal))
            {
                var succeeded = await HandleRootIntegrityAsync(db, jobQueueStore, job, cancellationToken);
                if (succeeded)
                {
                    await MarkSucceededAsync(jobQueueStore, job.JobId, cancellationToken);
                }
                return;
            }

            throw new InvalidOperationException($"Unknown job_type_key: {job.JobTypeKey}");
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
        }
    }

    private Task HandleDropboxCreateProjectStructureAsync(ClaimedJob job, CancellationToken cancellationToken)
    {
        var payload = JsonDocument.Parse(job.PayloadJson);
        var root = payload.RootElement;

        var projectId = root.TryGetProperty("projectId", out var projectIdElement) ? projectIdElement.GetString() : null;
        var clientId = root.TryGetProperty("clientId", out var clientIdElement) ? clientIdElement.GetString() : null;
        var templateKey =
            root.TryGetProperty("templateKey", out var templateKeyElement) ? templateKeyElement.GetString() : null;

        logger.LogInformation(
            "MGF.Worker: dry run job {JobId}: would create Dropbox structure (projectId={ProjectId}, clientId={ClientId}, templateKey={TemplateKey})",
            job.JobId,
            projectId,
            clientId,
            templateKey
        );

        return Task.CompletedTask;
    }

    private async Task<bool> HandleProjectBootstrapAsync(
        IJobQueueStore jobQueueStore,
        IBootstrapProjectUseCase useCase,
        ClaimedJob job,
        CancellationToken cancellationToken)
    {
        ProjectBootstrapPayload payload;
        try
        {
            payload = ProjectBootstrapper.ParsePayload(job.PayloadJson);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MGF.Worker: project.bootstrap payload invalid (job_id={JobId})", job.JobId);
            await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
            return false;
        }

        logger.LogInformation(
            "MGF.Worker: project.bootstrap start (job_id={JobId}, project_id={ProjectId})",
            job.JobId,
            payload.ProjectId
        );

        try
        {
            var request = new BootstrapProjectRequest(
                JobId: job.JobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                VerifyDomainRoots: payload.VerifyDomainRoots,
                CreateDomainRoots: payload.CreateDomainRoots,
                ProvisionProjectContainers: payload.ProvisionProjectContainers,
                AllowRepair: payload.AllowRepair,
                ForceSandbox: payload.ForceSandbox,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup
            );

            var result = await useCase.ExecuteAsync(request, cancellationToken);

            logger.LogInformation(
                "MGF.Worker: project.bootstrap completed (job_id={JobId}, project_id={ProjectId}, domains={Domains}, errors={HasErrors})",
                result.RunResult.JobId,
                result.RunResult.ProjectId,
                result.RunResult.Domains.Count,
                result.RunResult.HasErrors
            );

            if (result.RunResult.HasErrors)
            {
                await MarkFailedAsync(
                    jobQueueStore,
                    job,
                    new InvalidOperationException(result.RunResult.LastError ?? "project.bootstrap completed with provisioning errors."),
                    cancellationToken
                );
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "MGF.Worker: project.bootstrap failed (job_id={JobId}, project_id={ProjectId})",
                job.JobId,
                payload.ProjectId
            );
            await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
            return false;
        }
    }

    private async Task<bool> HandleProjectArchiveAsync(
        AppDbContext db,
        IJobQueueStore jobQueueStore,
        ClaimedJob job,
        CancellationToken cancellationToken)
    {
        ProjectArchivePayload payload;
        try
        {
            payload = ProjectArchiver.ParsePayload(job.PayloadJson);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MGF.Worker: project.archive payload invalid (job_id={JobId})", job.JobId);
            await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
            return false;
        }

        logger.LogInformation(
            "MGF.Worker: project.archive start (job_id={JobId}, project_id={ProjectId})",
            job.JobId,
            payload.ProjectId
        );

        try
        {
            var archiver = new ProjectArchiver(configuration);
            var result = await archiver.RunAsync(db, payload, job.JobId, cancellationToken);

            logger.LogInformation(
                "MGF.Worker: project.archive completed (job_id={JobId}, project_id={ProjectId}, domains={Domains}, errors={HasErrors})",
                result.JobId,
                result.ProjectId,
                result.Domains.Count,
                result.HasErrors
            );

            if (result.HasErrors)
            {
                await MarkFailedAsync(
                    jobQueueStore,
                    job,
                    new InvalidOperationException(result.LastError ?? "project.archive completed with errors."),
                    cancellationToken
                );
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "MGF.Worker: project.archive failed (job_id={JobId}, project_id={ProjectId})",
                job.JobId,
                payload.ProjectId
            );
            await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
            return false;
        }
    }

    private async Task<bool> HandleProjectDeliveryAsync(
        AppDbContext db,
        IJobQueueStore jobQueueStore,
        IProjectDeliveryStore deliveryStore,
        ClaimedJob job,
        CancellationToken cancellationToken)
    {
        ProjectDeliveryPayload payload;
        try
        {
            payload = ProjectDeliverer.ParsePayload(job.PayloadJson);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MGF.Worker: project.delivery payload invalid (job_id={JobId})", job.JobId);
            await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
            return false;
        }

        logger.LogInformation(
            "MGF.Worker: project.delivery start (job_id={JobId}, project_id={ProjectId})",
            job.JobId,
            payload.ProjectId
        );

        try
        {
            var deliverer = new ProjectDeliverer(configuration, logger: logger);
            var result = await deliverer.RunAsync(db, deliveryStore, payload, job.JobId, cancellationToken);

            logger.LogInformation(
                "MGF.Worker: project.delivery completed (job_id={JobId}, project_id={ProjectId}, domains={Domains}, errors={HasErrors})",
                result.JobId,
                result.ProjectId,
                result.Domains.Count,
                result.HasErrors
            );

            if (result.HasErrors)
            {
                await MarkFailedAsync(
                    jobQueueStore,
                    job,
                    new InvalidOperationException(result.LastError ?? "project.delivery completed with errors."),
                    cancellationToken
                );
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "MGF.Worker: project.delivery failed (job_id={JobId}, project_id={ProjectId})",
                job.JobId,
                payload.ProjectId
            );
            await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
            return false;
        }
    }

    private async Task<bool> HandleProjectDeliveryEmailAsync(
        AppDbContext db,
        IJobQueueStore jobQueueStore,
        IProjectDeliveryStore deliveryStore,
        ClaimedJob job,
        ISendDeliveryEmailUseCase useCase,
        CancellationToken cancellationToken)
    {
        ProjectDeliveryEmailPayload payload;
        try
        {
            payload = ProjectDeliveryEmailer.ParsePayload(job.PayloadJson);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MGF.Worker: project.delivery_email payload invalid (job_id={JobId})", job.JobId);
            await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
            return false;
        }

        logger.LogInformation(
            "MGF.Worker: project.delivery_email start (job_id={JobId}, project_id={ProjectId})",
            job.JobId,
            payload.ProjectId
        );

        try
        {
            var observedRecipients = new DeliveryEmailObservedRecipients(payload.ToEmails, payload.ReplyToEmail);
            var request = new SendDeliveryEmailRequest(
                ProjectId: payload.ProjectId,
                DeliveryVersionId: null,
                EditorInitials: payload.EditorInitials,
                Mode: DeliveryEmailMode.Send,
                ObservedRecipients: observedRecipients);
            var result = await useCase.ExecuteAsync(request, cancellationToken);

            if (!string.Equals(result.Status, "sent", StringComparison.OrdinalIgnoreCase))
            {
                await MarkFailedAsync(
                    jobQueueStore,
                    job,
                    new InvalidOperationException(result.Error ?? "project.delivery_email failed."),
                    cancellationToken
                );
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "MGF.Worker: project.delivery_email failed (job_id={JobId}, project_id={ProjectId})",
                job.JobId,
                payload.ProjectId
            );
            await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
            return false;
        }
    }

    private async Task<bool> HandleRootIntegrityAsync(
        AppDbContext db,
        IJobQueueStore jobQueueStore,
        ClaimedJob job,
        CancellationToken cancellationToken)
    {
        RootIntegrityPayload payload;
        try
        {
            payload = RootIntegrityPayload.Parse(job.PayloadJson);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MGF.Worker: domain.root_integrity payload invalid (job_id={JobId})", job.JobId);
            await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
            return false;
        }

        logger.LogInformation(
            "MGF.Worker: domain.root_integrity start (job_id={JobId}, provider={ProviderKey}, root_key={RootKey}, mode={Mode}, dryRun={DryRun})",
            job.JobId,
            payload.ProviderKey,
            payload.RootKey,
            payload.Mode,
            payload.DryRun
        );

        try
        {
            var checker = new RootIntegrityChecker(configuration);
            var result = await checker.RunAsync(db, payload, job.JobId, cancellationToken);

            var updatedPayload = RootIntegrityChecker.BuildJobPayloadJson(payload, result);
            await UpdateJobPayloadAsync(jobQueueStore, job.JobId, updatedPayload, cancellationToken);

            logger.LogInformation(
                "MGF.Worker: domain.root_integrity completed (job_id={JobId}, provider={ProviderKey}, root_key={RootKey}, errors={HasErrors})",
                job.JobId,
                result.ProviderKey,
                result.RootKey,
                result.HasErrors
            );

            if (result.HasErrors)
            {
                await MarkFailedAsync(
                    jobQueueStore,
                    job,
                    new InvalidOperationException(result.Errors.FirstOrDefault() ?? "domain.root_integrity completed with errors."),
                    cancellationToken
                );
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MGF.Worker: domain.root_integrity failed (job_id={JobId})", job.JobId);
            await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
            return false;
        }
    }

    private async Task<bool> HandleSquareWebhookEventProcessAsync(
        AppDbContext db,
        IJobQueueStore jobQueueStore,
        ClaimedJob job,
        ISquareWebhookStore webhookStore,
        CancellationToken cancellationToken
    )
    {
        if (!TryExtractSquareEventId(job.PayloadJson, out var squareEventId, out var payloadError))
        {
            logger.LogWarning(
                "MGF.Worker: Square webhook job payload invalid; failing job (job_id={JobId}, error={Error})",
                job.JobId,
                payloadError
            );
            await MarkFailedAsync(jobQueueStore, job, new InvalidOperationException(payloadError), cancellationToken);
            return false;
        }

        logger.LogInformation(
            "MGF.Worker: Square webhook event processing start (job_id={JobId}, square_event_id={SquareEventId})",
            job.JobId,
            squareEventId
        );

        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var evt = await db.SquareWebhookEvents.FromSqlInterpolated(
                        $"""
                        SELECT *
                        FROM public.square_webhook_events
                        WHERE square_event_id = {squareEventId}
                        FOR UPDATE
                        """
                    )
                    .SingleOrDefaultAsync(cancellationToken);

                if (evt is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    logger.LogWarning(
                        "MGF.Worker: square_webhook_events row not found; failing job (job_id={JobId}, square_event_id={SquareEventId})",
                        job.JobId,
                        squareEventId
                    );
                    await MarkFailedAsync(
                        jobQueueStore,
                        job,
                        new InvalidOperationException(
                            $"square_webhook_events row not found for square_event_id={squareEventId}."
                        ),
                        cancellationToken
                    );
                    return false;
                }

                var parsed = ParseSquareWebhookEvent(evt);

                if (
                    evt.ProcessedAt is not null
                    || string.Equals(evt.Status, "processed", StringComparison.OrdinalIgnoreCase)
                )
                {
                    logger.LogInformation(
                        "MGF.Worker: Square webhook event already processed; no-op (square_event_id={SquareEventId}, event_type={EventType})",
                        parsed.SquareEventId,
                        parsed.EventType
                    );
                    await transaction.CommitAsync(cancellationToken);
                    return true;
                }

                var isMoneyEvent = IsPaymentRelated(parsed.EventType, parsed.ObjectType)
                    || IsInvoiceRelated(parsed.EventType, parsed.ObjectType);

                var now = DateTimeOffset.UtcNow;
                var outcome = isMoneyEvent ? "ready_for_reconcile" : "review_queue_unhandled_event_type";

                if (!isMoneyEvent)
                {
                    var error =
                        $"Unhandled Square webhook event type: {parsed.EventType} (object_type={parsed.ObjectType ?? "unknown"}, object_id={parsed.ObjectId ?? "unknown"})";

                    await UpsertSquareSyncReviewQueueItemAsync(
                        db,
                        reviewType: SquareReviewTypeUnhandledWebhookEventType,
                        processorKey: SquareProcessorKey,
                        processorPaymentId: squareEventId,
                        squareEventId: parsed.SquareEventId,
                        squareCustomerId: null,
                        payloadJson: evt.Payload.GetRawText(),
                        error: error,
                        cancellationToken: cancellationToken
                    );
                }

                evt.Status = "processed";
                evt.ProcessedAt = now;
                evt.Error = null;

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation(
                    "MGF.Worker: Square webhook event processing end (square_event_id={SquareEventId}, event_type={EventType}, outcome={Outcome})",
                    parsed.SquareEventId,
                    parsed.EventType,
                    outcome
                );

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogWarning(
                    "MGF.Worker: Square webhook event processing failed (job_id={JobId}, square_event_id={SquareEventId})",
                    job.JobId,
                    squareEventId
                );
                await MarkSquareWebhookEventFailedAsync(webhookStore, squareEventId, ex, cancellationToken);
                await MarkFailedAsync(jobQueueStore, job, ex, cancellationToken);
                return false;
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static Task UpdateJobPayloadAsync(
        IJobQueueStore jobQueueStore,
        string jobId,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        return jobQueueStore.UpdateJobPayloadAsync(jobId, payloadJson, cancellationToken);
    }

    private async Task HandleSquareReconcilePaymentsAsync(AppDbContext db, ClaimedJob job, CancellationToken cancellationToken)
    {
        var accessToken = configuration["Square:AccessToken"];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Missing config value Square:AccessToken.");
        }

        var locationIds = GetSquareLocationIds(configuration);
        if (locationIds.Count == 0)
        {
            throw new InvalidOperationException("Missing config value Square:LocationIds.");
        }

        var daysBack = GetInt(configuration["Square:PaymentsReconcileDaysBack"], defaultValue: 14);
        var pageSize = Math.Clamp(GetInt(configuration["Square:PaymentsReconcilePageSize"], defaultValue: 100), 1, 200);
        var overlapSeconds = Math.Clamp(GetInt(configuration["Square:PaymentsReconcileOverlapSeconds"], defaultValue: 60), 0, 3600);
        var maxPages = Math.Clamp(GetInt(configuration["Square:PaymentsReconcileMaxPages"], defaultValue: 100), 1, 10_000);

        var now = DateTimeOffset.UtcNow;

        foreach (var locationId in locationIds)
        {
            var reconcileKey = $"payments:{locationId}";

            var existingCursor = await db.SquareReconcileCursors.AsNoTracking()
                .Where(c => c.ReconcileKey == reconcileKey)
                .Select(c => (DateTimeOffset?)c.CursorAt)
                .FirstOrDefaultAsync(cancellationToken);

            var beginTime = existingCursor ?? now.AddDays(-daysBack);
            if (existingCursor is not null && overlapSeconds > 0)
            {
                beginTime = beginTime.AddSeconds(-overlapSeconds);
            }

            beginTime = beginTime.ToUniversalTime();

            logger.LogInformation(
                "MGF.Worker: Square reconcile payments starting (location_id={LocationId}, begin_time={BeginTime:O}, page_size={PageSize})",
                locationId,
                beginTime,
                pageSize
            );

            string? cursor = null;
            var pages = 0;
            var paymentsSeen = 0;
            var jobsEnqueued = 0;

            var maxSeenTimestamp = existingCursor ?? beginTime;

            do
            {
                var page = await squareApiClient.ListPaymentsAsync(
                    accessToken: accessToken,
                    locationId: locationId,
                    beginTimeUtc: beginTime,
                    cursor: cursor,
                    limit: pageSize,
                    cancellationToken: cancellationToken
                );

                pages++;

                if (page.Payments.Count > 0)
                {
                    var paymentIds = page.Payments
                        .Select(p => p.PaymentId)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();

                    paymentsSeen += paymentIds.Count;
                    jobsEnqueued += await EnqueueSquarePaymentUpsertJobsAsync(db, paymentIds, cancellationToken);

                    foreach (var item in page.Payments)
                    {
                        if (item.UpdatedAtUtc is not null && item.UpdatedAtUtc.Value > maxSeenTimestamp)
                        {
                            maxSeenTimestamp = item.UpdatedAtUtc.Value;
                        }
                    }
                }

                cursor = page.Cursor;
            } while (!string.IsNullOrWhiteSpace(cursor) && pages < maxPages);

            if (maxSeenTimestamp > (existingCursor ?? DateTimeOffset.MinValue))
            {
                await UpsertReconcileCursorAsync(db, reconcileKey, maxSeenTimestamp, cancellationToken);
            }

            logger.LogInformation(
                "MGF.Worker: Square reconcile payments finished (location_id={LocationId}, pages={Pages}, payments_seen={PaymentsSeen}, jobs_enqueued={JobsEnqueued}, cursor_at={CursorAt:O})",
                locationId,
                pages,
                paymentsSeen,
                jobsEnqueued,
                maxSeenTimestamp
            );
        }
    }

    private async Task HandleSquarePaymentUpsertAsync(
        AppDbContext db,
        ICounterAllocator counterAllocator,
        ClaimedJob job,
        CancellationToken cancellationToken)
    {
        var accessToken = configuration["Square:AccessToken"];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Missing config value Square:AccessToken.");
        }

        var squarePaymentId = ExtractSquarePaymentId(job.PayloadJson);

        var now = DateTimeOffset.UtcNow;
        var paymentDetail = await squareApiClient.GetPaymentAsync(accessToken, squarePaymentId, cancellationToken);

        var parsed = ParseSquarePaymentFromApi(paymentDetail, now);

        var clientId = await TryResolveClientIdFromSquareCustomerIdAsync(db, parsed.SquareCustomerId, cancellationToken);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            var error = string.IsNullOrWhiteSpace(parsed.SquareCustomerId)
                ? "Square payment missing customer_id; cannot map to internal client."
                : $"No internal client mapped for square_customer_id={parsed.SquareCustomerId}.";

            using var paymentDoc = JsonDocument.Parse(paymentDetail.RawPaymentJson);
            var payloadJson = JsonSerializer.Serialize(
                new
                {
                    source = "square.payment.upsert",
                    square_payment_id = paymentDetail.PaymentId,
                    square_customer_id = paymentDetail.CustomerId,
                    square_location_id = paymentDetail.LocationId,
                    payment = paymentDoc.RootElement,
                }
            );

            await UpsertSquareSyncReviewQueueItemAsync(
                db,
                reviewType: SquareReviewTypeMissingClientMapping,
                processorKey: SquareProcessorKey,
                processorPaymentId: parsed.SquarePaymentId,
                squareEventId: $"payment:{parsed.SquarePaymentId}",
                squareCustomerId: parsed.SquareCustomerId,
                payloadJson: payloadJson,
                error: error,
                cancellationToken: cancellationToken
            );

            logger.LogWarning(
                "MGF.Worker: Square payment upsert needs review (payment_id={PaymentId}, customer_id={CustomerId}): {Error}",
                parsed.SquarePaymentId,
                parsed.SquareCustomerId,
                error
            );

            return;
        }

        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var ledgerProjectId = await EnsureLedgerProjectIdAsync(
                    db,
                    counterAllocator,
                    clientId,
                    now,
                    cancellationToken);

                await UpsertSquarePaymentAsync(
                    db,
                    counterAllocator,
                    parsed,
                    clientId: clientId,
                    ledgerProjectId: ledgerProjectId,
                    now: now,
                    cancellationToken: cancellationToken
                );

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        logger.LogInformation(
            "MGF.Worker: Square payment upserted (payment_id={PaymentId}, client_id={ClientId})",
            parsed.SquarePaymentId,
            clientId
        );
    }

    private static ParsedSquarePayment ParseSquarePaymentFromApi(SquareApiClient.SquarePaymentDetail detail, DateTimeOffset nowUtc)
    {
        var statusKey = MapPaymentStatusKey(detail.RawStatus);

        var issuedAtUtc = detail.CreatedAtUtc ?? detail.UpdatedAtUtc ?? nowUtc;

        DateTimeOffset? capturedAtUtc = null;
        if (statusKey == "captured")
        {
            capturedAtUtc = detail.UpdatedAtUtc ?? detail.CreatedAtUtc ?? nowUtc;
        }

        return new ParsedSquarePayment(
            SquarePaymentId: detail.PaymentId,
            SquareCustomerId: detail.CustomerId,
            Amount: ToMoney(detail.AmountCents),
            CurrencyCode: NormalizeCurrencyCode(detail.CurrencyCode) ?? "USD",
            PaymentStatusKey: statusKey,
            CapturedAtUtc: capturedAtUtc,
            RefundedAtUtc: null,
            RefundedAmount: null,
            IssuedAtUtc: issuedAtUtc
        );
    }

    private static async Task UpsertReconcileCursorAsync(
        AppDbContext db,
        string reconcileKey,
        DateTimeOffset cursorAtUtc,
        CancellationToken cancellationToken
    )
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO public.square_reconcile_cursors (reconcile_key, cursor_at)
            VALUES ({reconcileKey}, {cursorAtUtc})
            ON CONFLICT (reconcile_key) DO UPDATE
            SET cursor_at = GREATEST(square_reconcile_cursors.cursor_at, EXCLUDED.cursor_at),
                updated_at = now();
            """,
            cancellationToken
        );
    }

    private static async Task<int> EnqueueSquarePaymentUpsertJobsAsync(
        AppDbContext db,
        IReadOnlyList<string> paymentIds,
        CancellationToken cancellationToken
    )
    {
        if (paymentIds.Count == 0)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;

        await using var cmd = db.Database.GetDbConnection().CreateCommand();

        cmd.CommandText =
            $"""
            INSERT INTO public.jobs (job_id, job_type_key, payload, status_key, run_after, entity_type_key, entity_key)
            VALUES
              {string.Join(
                  ",\n  ",
                  paymentIds.Select((_, i) => $"(@job_id_{i}, @job_type_key, @payload_{i}::jsonb, @status_key, @run_after, @entity_type_key, @entity_key_{i})")
              )};
            """;

        AddParameter(cmd, "@job_type_key", "square.payment.upsert");
        AddParameter(cmd, "@status_key", "queued");
        AddParameter(cmd, "@run_after", now);
        AddParameter(cmd, "@entity_type_key", "square_payment");

        for (var i = 0; i < paymentIds.Count; i++)
        {
            var paymentId = paymentIds[i];
            var jobId = EntityIds.NewWithPrefix("job");
            var payloadJson = JsonSerializer.Serialize(new { square_payment_id = paymentId });

            AddParameter(cmd, $"@job_id_{i}", jobId);
            AddParameter(cmd, $"@payload_{i}", payloadJson);
            AddParameter(cmd, $"@entity_key_{i}", paymentId);
        }

        if (cmd.Connection?.State != System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static string ExtractSquarePaymentId(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (
                string.Equals(prop.Name, "square_payment_id", StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop.Name, "payment_id", StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop.Name, "squarePaymentId", StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop.Name, "paymentId", StringComparison.OrdinalIgnoreCase)
            )
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var value = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }

        throw new InvalidOperationException("Missing square_payment_id in job payload.");
    }

    private static IReadOnlyList<string> GetSquareLocationIds(IConfiguration configuration)
    {
        var fromSection = configuration.GetSection("Square:LocationIds")
            .GetChildren()
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .ToList();

        if (fromSection.Count > 0)
        {
            return fromSection;
        }

        var raw = configuration["Square:LocationIds"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split([',', ';', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int GetInt(string? value, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : defaultValue;
    }

    private sealed record ParsedSquareWebhookEvent(
        string SquareEventId,
        string EventType,
        string? ObjectType,
        string? ObjectId,
        string? LocationId
    );

    private static bool TryExtractSquareEventId(string payloadJson, out string squareEventId, out string error)
    {
        squareEventId = string.Empty;

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException ex)
        {
            error = $"Job payload is not valid JSON: {ex.Message}";
            return false;
        }

        using (doc)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (
                    string.Equals(prop.Name, "square_event_id", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(prop.Name, "squareEventId", StringComparison.OrdinalIgnoreCase)
                )
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            squareEventId = value;
                            error = string.Empty;
                            return true;
                        }
                    }
                }
            }
        }

        error = "Job payload missing square_event_id.";
        return false;
    }

    private static ParsedSquareWebhookEvent ParseSquareWebhookEvent(SquareWebhookEvent evt)
    {
        var root = evt.Payload;

        var squareEventId = evt.SquareEventId;
        var eventType = string.IsNullOrWhiteSpace(evt.EventType) ? (GetString(root, "type") ?? "unknown") : evt.EventType;
        var objectType = string.IsNullOrWhiteSpace(evt.ObjectType) ? null : evt.ObjectType;
        var objectId = string.IsNullOrWhiteSpace(evt.ObjectId) ? null : evt.ObjectId;
        var locationId = string.IsNullOrWhiteSpace(evt.LocationId) ? null : evt.LocationId;

        if (TryGetObject(root, "data", out var data) && TryGetObject(data, "object", out var obj))
        {
            objectType ??= GetString(obj, "type");
            objectId ??= GetString(obj, "id");
            locationId ??= GetString(obj, "location_id") ?? GetString(obj, "locationId");
        }

        locationId ??= GetString(root, "location_id") ?? GetString(root, "locationId");

        return new ParsedSquareWebhookEvent(squareEventId, eventType, objectType, objectId, locationId);
    }

    private static bool IsPaymentRelated(string? eventType, string? objectType)
    {
        return Contains(eventType, "payment")
            || Contains(eventType, "refund")
            || string.Equals(objectType, "payment", StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectType, "refund", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInvoiceRelated(string? eventType, string? objectType)
    {
        return Contains(eventType, "invoice") || string.Equals(objectType, "invoice", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string? value, string token)
    {
        return value?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private async Task ProcessSquarePaymentEventAsync(
        AppDbContext db,
        ICounterAllocator counterAllocator,
        SquareWebhookEvent row,
        ParsedSquareWebhookEvent parsed,
        string jobId,
        CancellationToken cancellationToken
    )
    {
        var now = DateTimeOffset.UtcNow;

        if (!TryParseSquarePayment(row.Payload, parsed, now, out var payment))
        {
            throw new InvalidOperationException($"Square webhook {parsed.SquareEventId} did not contain a parsable payment object.");
        }

        var clientId = await TryResolveClientIdFromSquareCustomerIdAsync(db, payment.SquareCustomerId, cancellationToken);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            var error = string.IsNullOrWhiteSpace(payment.SquareCustomerId)
                ? "Square payment payload missing customer_id; cannot map to internal client."
                : $"No internal client mapped for square_customer_id={payment.SquareCustomerId}.";

            throw new SquareSyncReviewNeededException(
                reviewType: SquareReviewTypeMissingClientMapping,
                processorKey: SquareProcessorKey,
                processorPaymentId: payment.SquarePaymentId,
                squareEventId: parsed.SquareEventId,
                squareCustomerId: payment.SquareCustomerId,
                payloadJson: row.Payload.GetRawText(),
                message: error
            );
        }

        var ledgerProjectId = await EnsureLedgerProjectIdAsync(
            db,
            counterAllocator,
            clientId,
            now,
            cancellationToken);

        await UpsertSquarePaymentAsync(
            db,
            counterAllocator,
            payment,
            clientId: clientId,
            ledgerProjectId: ledgerProjectId,
            now: now,
            cancellationToken: cancellationToken
        );

        await WriteSquareWebhookActivityAsync(db, row, parsed, jobId, topicKey: "payments", cancellationToken);
    }

    private async Task ProcessSquareInvoiceEventAsync(
        AppDbContext db,
        SquareWebhookEvent row,
        ParsedSquareWebhookEvent parsed,
        string jobId,
        CancellationToken cancellationToken
    )
    {
        await WriteSquareWebhookActivityAsync(db, row, parsed, jobId, topicKey: "invoices", cancellationToken);
    }

    private async Task ProcessSquareOtherEventAsync(
        AppDbContext db,
        SquareWebhookEvent row,
        ParsedSquareWebhookEvent parsed,
        string jobId,
        CancellationToken cancellationToken
    )
    {
        await WriteSquareWebhookActivityAsync(db, row, parsed, jobId, topicKey: "daemon.sync", cancellationToken);
    }

    private sealed record ParsedSquarePayment(
        string SquarePaymentId,
        string? SquareCustomerId,
        decimal Amount,
        string CurrencyCode,
        string PaymentStatusKey,
        DateTimeOffset? CapturedAtUtc,
        DateTimeOffset? RefundedAtUtc,
        decimal? RefundedAmount,
        DateTimeOffset IssuedAtUtc
    );

    private sealed class SquareSyncReviewNeededException : Exception
    {
        public SquareSyncReviewNeededException(
            string reviewType,
            string processorKey,
            string processorPaymentId,
            string squareEventId,
            string? squareCustomerId,
            string payloadJson,
            string message
        )
            : base(message)
        {
            ReviewType = reviewType;
            ProcessorKey = processorKey;
            ProcessorPaymentId = processorPaymentId;
            SquareEventId = squareEventId;
            SquareCustomerId = squareCustomerId;
            PayloadJson = payloadJson;
        }

        public string ReviewType { get; }
        public string ProcessorKey { get; }
        public string ProcessorPaymentId { get; }
        public string SquareEventId { get; }
        public string? SquareCustomerId { get; }
        public string PayloadJson { get; }
    }

    private static bool TryParseSquarePayment(
        JsonElement root,
        ParsedSquareWebhookEvent parsed,
        DateTimeOffset nowUtc,
        out ParsedSquarePayment payment
    )
    {
        if (!TryGetObject(root, "data", out var data) || !TryGetObject(data, "object", out var obj))
        {
            payment = default!;
            return false;
        }

        if (TryGetObject(obj, "payment", out var paymentObj))
        {
            return TryParseSquarePaymentObject(paymentObj, data, parsed, nowUtc, out payment);
        }

        // Some payloads may have the payment object directly under data.object.
        return TryParseSquarePaymentObject(obj, data, parsed, nowUtc, out payment);
    }

    private static bool TryParseSquarePaymentObject(
        JsonElement paymentObj,
        JsonElement data,
        ParsedSquareWebhookEvent parsed,
        DateTimeOffset nowUtc,
        out ParsedSquarePayment payment
    )
    {
        var paymentId = GetString(paymentObj, "id") ?? GetString(data, "id") ?? parsed.ObjectId;
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            payment = default!;
            return false;
        }

        var customerId = GetString(paymentObj, "customer_id") ?? GetString(paymentObj, "customerId");

        if (!TryGetObject(paymentObj, "amount_money", out var amountMoney))
        {
            payment = default!;
            return false;
        }

        var amountCents = GetLong(amountMoney, "amount");
        if (amountCents is null || amountCents.Value <= 0)
        {
            payment = default!;
            return false;
        }

        var currencyCode = NormalizeCurrencyCode(GetString(amountMoney, "currency")) ?? "USD";

        var rawStatus = GetString(paymentObj, "status") ?? "UNKNOWN";
        var paymentStatusKey = MapPaymentStatusKey(rawStatus);

        var createdAt = ParseUtcTimestamp(GetString(paymentObj, "created_at") ?? GetString(paymentObj, "createdAt"));
        var updatedAt = ParseUtcTimestamp(GetString(paymentObj, "updated_at") ?? GetString(paymentObj, "updatedAt"));

        var issuedAtUtc = createdAt ?? updatedAt ?? nowUtc;

        DateTimeOffset? capturedAtUtc = null;
        if (paymentStatusKey == "captured")
        {
            capturedAtUtc = updatedAt ?? createdAt ?? nowUtc;
        }

        // Refund handling will be implemented via refund webhooks; keep fields null here.
        payment = new ParsedSquarePayment(
            SquarePaymentId: paymentId,
            SquareCustomerId: customerId,
            Amount: ToMoney(amountCents.Value),
            CurrencyCode: currencyCode,
            PaymentStatusKey: paymentStatusKey,
            CapturedAtUtc: capturedAtUtc,
            RefundedAtUtc: null,
            RefundedAmount: null,
            IssuedAtUtc: issuedAtUtc
        );
        return true;
    }

    private static string MapPaymentStatusKey(string squareStatus)
    {
        if (squareStatus.Contains("APPROV", StringComparison.OrdinalIgnoreCase))
        {
            return "authorized";
        }

        if (squareStatus.Contains("PEND", StringComparison.OrdinalIgnoreCase))
        {
            return "pending";
        }

        if (
            squareStatus.Contains("FAIL", StringComparison.OrdinalIgnoreCase)
            || squareStatus.Contains("CANCEL", StringComparison.OrdinalIgnoreCase)
        )
        {
            return "failed";
        }

        return "captured";
    }

    private static string MapInvoiceStatusKey(string paymentStatusKey)
    {
        return paymentStatusKey switch
        {
            "captured" => "paid",
            "refunded" => "refunded",
            "failed" => "void",
            _ => "unpaid",
        };
    }

    private static decimal ToMoney(long cents) => cents / 100m;

    private static string? NormalizeCurrencyCode(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return null;
        }

        var normalized = currencyCode.Trim().ToUpperInvariant();
        return normalized.Length == 3 ? normalized : null;
    }

    private static DateTimeOffset? ParseUtcTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (
            DateTimeOffset.TryParse(
                value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto
            )
        )
        {
            return dto.ToUniversalTime();
        }

        return null;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out var i))
            {
                return i;
            }

            if (property.Value.ValueKind == JsonValueKind.String && long.TryParse(property.Value.GetString(), out var s))
            {
                return s;
            }
        }

        return null;
    }

    private static async Task<string?> TryResolveClientIdFromSquareCustomerIdAsync(
        AppDbContext db,
        string? squareCustomerId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(squareCustomerId))
        {
            return null;
        }

        var clientIntegrationsSquare = db.Set<Dictionary<string, object>>("client_integrations_square");

        return await clientIntegrationsSquare.AsNoTracking()
            .Where(cis => EF.Property<string?>(cis, "square_customer_id") == squareCustomerId)
            .Select(cis => EF.Property<string>(cis, "client_id"))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> EnsureLedgerProjectIdAsync(
        AppDbContext db,
        ICounterAllocator counterAllocator,
        string clientId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken
    )
    {
        var existing = await db.Projects.AsNoTracking()
            .Where(p => p.ClientId == clientId && p.Name == LedgerProjectName)
            .Select(p => p.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var projectId = EntityIds.NewProjectId();
        var projectCode = await counterAllocator.AllocateProjectCodeAsync(cancellationToken);

        db.Projects.Add(
            new Project(
                projectId: projectId,
                projectCode: projectCode,
                clientId: clientId,
                name: LedgerProjectName,
                statusKey: "active",
                phaseKey: "planning",
                dataProfile: "real"
            )
        );

        await db.SaveChangesAsync(cancellationToken);

        return projectId;
    }

    private static async Task UpsertSquarePaymentAsync(
        AppDbContext db,
        ICounterAllocator counterAllocator,
        ParsedSquarePayment payment,
        string clientId,
        string ledgerProjectId,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var payments = db.Set<Dictionary<string, object>>("payments");
        var invoices = db.Set<Dictionary<string, object>>("invoices");

        var existingPayment = await payments.SingleOrDefaultAsync(
            p =>
                EF.Property<string?>(p, "processor_key") == SquareProcessorKey
                && EF.Property<string?>(p, "processor_payment_id") == payment.SquarePaymentId,
            cancellationToken
        );

        var invoiceStatusKey = MapInvoiceStatusKey(payment.PaymentStatusKey);

        if (existingPayment is not null)
        {
            var invoiceId = GetRequiredString(existingPayment, "invoice_id");

            var invoiceRow = await invoices.SingleOrDefaultAsync(
                i => EF.Property<string>(i, "invoice_id") == invoiceId,
                cancellationToken
            );

            if (invoiceRow is null)
            {
                throw new InvalidOperationException($"payments.invoice_id={invoiceId} not found in invoices.");
            }

            _ = ApplyInvoiceLinkFields(
                invoiceRow,
                clientId: clientId,
                projectId: ledgerProjectId,
                statusKey: invoiceStatusKey,
                issuedAt: payment.IssuedAtUtc,
                paidAt: payment.PaymentStatusKey == "captured" ? payment.CapturedAtUtc : null,
                currencyCode: payment.CurrencyCode,
                now: now
            );

            _ = ApplyPaymentFields(
                existingPayment,
                statusKey: payment.PaymentStatusKey,
                capturedAt: payment.CapturedAtUtc,
                refundedAmount: payment.RefundedAmount,
                refundedAt: payment.RefundedAtUtc,
                currencyCode: payment.CurrencyCode,
                amount: payment.Amount,
                now: now,
                squarePaymentId: payment.SquarePaymentId
            );

            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var issuedAt = payment.IssuedAtUtc;
        var year2 = (short)(issuedAt.Year % 100);
        var invoiceNumber = await counterAllocator.AllocateInvoiceNumberAsync(year2, cancellationToken);
        var invoiceIdNew = EntityIds.NewWithPrefix("inv");

        await invoices.AddAsync(
            CreateInvoiceRow(
                invoiceId: invoiceIdNew,
                invoiceNumber: invoiceNumber,
                clientId: clientId,
                projectId: ledgerProjectId,
                statusKey: invoiceStatusKey,
                issuedAt: issuedAt,
                paidAt: payment.PaymentStatusKey == "captured" ? payment.CapturedAtUtc : null,
                currencyCode: payment.CurrencyCode,
                amount: payment.Amount,
                now: now,
                squarePaymentId: payment.SquarePaymentId,
                squareCustomerId: payment.SquareCustomerId
            ),
            cancellationToken
        );

        var paymentId = EntityIds.NewWithPrefix("pay");
        await payments.AddAsync(
            CreatePaymentRow(
                paymentId: paymentId,
                invoiceId: invoiceIdNew,
                statusKey: payment.PaymentStatusKey,
                capturedAt: payment.CapturedAtUtc,
                refundedAmount: payment.RefundedAmount,
                refundedAt: payment.RefundedAtUtc,
                currencyCode: payment.CurrencyCode,
                amount: payment.Amount,
                now: now,
                squarePaymentId: payment.SquarePaymentId,
                squareCustomerId: payment.SquareCustomerId
            ),
            cancellationToken
        );

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, object> CreateInvoiceRow(
        string invoiceId,
        string invoiceNumber,
        string clientId,
        string projectId,
        string statusKey,
        DateTimeOffset issuedAt,
        DateTimeOffset? paidAt,
        string currencyCode,
        decimal amount,
        DateTimeOffset now,
        string squarePaymentId,
        string? squareCustomerId
    )
    {
        var noteSuffix = squareCustomerId is null ? string.Empty : $"; square_customer_id={squareCustomerId}";

        return new Dictionary<string, object>
        {
            ["invoice_id"] = invoiceId,
            ["client_id"] = clientId,
            ["project_id"] = projectId,
            ["invoice_number"] = invoiceNumber,
            ["currency_code"] = currencyCode,
            ["status_key"] = statusKey,
            ["issued_at"] = issuedAt,
            ["due_at"] = null!,
            ["paid_at"] = paidAt!,
            ["refunded_at"] = null!,
            ["payment_method_key"] = SquarePaymentMethodKey,
            ["subtotal_amount"] = amount,
            ["tax_rate"] = null!,
            ["tax_amount"] = 0m,
            ["total_amount"] = amount,
            ["notes"] = $"square-webhook payment: payment_id={squarePaymentId}{noteSuffix}",
            ["data_profile"] = "real",
            ["created_at"] = now,
            ["updated_at"] = null!,
        };
    }

    private static Dictionary<string, object> CreatePaymentRow(
        string paymentId,
        string invoiceId,
        string statusKey,
        DateTimeOffset? capturedAt,
        decimal? refundedAmount,
        DateTimeOffset? refundedAt,
        string currencyCode,
        decimal amount,
        DateTimeOffset now,
        string squarePaymentId,
        string? squareCustomerId
    )
    {
        var noteSuffix = squareCustomerId is null ? string.Empty : $"; square_customer_id={squareCustomerId}";

        return new Dictionary<string, object>
        {
            ["payment_id"] = paymentId,
            ["invoice_id"] = invoiceId,
            ["amount"] = amount,
            ["currency_code"] = currencyCode,
            ["status_key"] = statusKey,
            ["captured_at"] = capturedAt!,
            ["refunded_amount"] = refundedAmount!,
            ["refunded_at"] = refundedAt!,
            ["method_key"] = SquarePaymentMethodKey,
            ["processor_key"] = SquareProcessorKey,
            ["processor_payment_id"] = squarePaymentId,
            ["processor_refund_id"] = null!,
            ["recorded_by_person_id"] = null!,
            ["source"] = "square_webhook",
            ["notes"] = $"square-webhook payment: payment_id={squarePaymentId}{noteSuffix}",
            ["data_profile"] = "real",
            ["created_at"] = now,
            ["updated_at"] = null!,
        };
    }

    private static bool ApplyInvoiceLinkFields(
        Dictionary<string, object> invoiceRow,
        string clientId,
        string projectId,
        string statusKey,
        DateTimeOffset issuedAt,
        DateTimeOffset? paidAt,
        string currencyCode,
        DateTimeOffset now
    )
    {
        var changed = false;

        changed |= SetIfDifferent(invoiceRow, "client_id", clientId);
        changed |= SetIfDifferent(invoiceRow, "project_id", projectId);
        changed |= SetIfDifferent(invoiceRow, "status_key", statusKey);
        changed |= SetIfDifferent(invoiceRow, "currency_code", currencyCode);
        changed |= SetIfDifferent(invoiceRow, "issued_at", issuedAt);
        changed |= SetIfDifferent(invoiceRow, "paid_at", paidAt!);
        changed |= SetIfDifferent(invoiceRow, "payment_method_key", SquarePaymentMethodKey);

        if (changed)
        {
            _ = SetIfDifferent(invoiceRow, "updated_at", now);
        }

        return changed;
    }

    private static bool ApplyPaymentFields(
        Dictionary<string, object> paymentRow,
        string statusKey,
        DateTimeOffset? capturedAt,
        decimal? refundedAmount,
        DateTimeOffset? refundedAt,
        string currencyCode,
        decimal amount,
        DateTimeOffset now,
        string squarePaymentId
    )
    {
        var changed = false;

        changed |= SetIfDifferent(paymentRow, "status_key", statusKey);
        changed |= SetIfDifferent(paymentRow, "captured_at", capturedAt!);
        changed |= SetIfDifferent(paymentRow, "refunded_amount", refundedAmount!);
        changed |= SetIfDifferent(paymentRow, "refunded_at", refundedAt!);
        changed |= SetIfDifferent(paymentRow, "currency_code", currencyCode);
        changed |= SetIfDifferent(paymentRow, "amount", amount);
        changed |= SetIfDifferent(paymentRow, "method_key", SquarePaymentMethodKey);
        changed |= SetIfDifferent(paymentRow, "processor_key", SquareProcessorKey);
        changed |= SetIfDifferent(paymentRow, "processor_payment_id", squarePaymentId);
        changed |= SetIfDifferent(paymentRow, "source", "square_webhook");

        if (changed)
        {
            _ = SetIfDifferent(paymentRow, "updated_at", now);
        }

        return changed;
    }

    private static async Task UpsertSquareSyncReviewQueueItemAsync(
        AppDbContext db,
        string reviewType,
        string processorKey,
        string processorPaymentId,
        string squareEventId,
        string? squareCustomerId,
        string payloadJson,
        string error,
        CancellationToken cancellationToken
    )
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO public.square_sync_review_queue
              (review_type, processor_key, processor_payment_id, square_event_id, square_customer_id, status, payload, error)
            VALUES
              ({reviewType}, {processorKey}, {processorPaymentId}, {squareEventId}, {squareCustomerId}, {"open"}, {payloadJson}::jsonb, {error})
            ON CONFLICT (review_type, processor_key, processor_payment_id) DO UPDATE
            SET updated_at = now(),
                status = {"open"},
                square_event_id = EXCLUDED.square_event_id,
                square_customer_id = COALESCE(EXCLUDED.square_customer_id, square_sync_review_queue.square_customer_id),
                payload = EXCLUDED.payload,
                error = EXCLUDED.error;
            """,
            cancellationToken
        );
    }

    private static bool SetIfDifferent(Dictionary<string, object> row, string key, object value)
    {
        if (row.TryGetValue(key, out var existing) && Equals(existing, value))
        {
            return false;
        }

        row[key] = value;
        return true;
    }

    private static string GetRequiredString(Dictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null || value is DBNull)
        {
            throw new InvalidOperationException($"Missing required column {key}.");
        }

        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        throw new InvalidOperationException($"Missing required column {key}.");
    }

    private async Task WriteSquareWebhookActivityAsync(
        AppDbContext db,
        SquareWebhookEvent row,
        ParsedSquareWebhookEvent parsed,
        string jobId,
        string topicKey,
        CancellationToken cancellationToken
    )
    {
        var activityId = EntityIds.NewWithPrefix("evt");

        var payloadJson = JsonSerializer.Serialize(
            new
            {
                square_webhook_event_id = row.SquareWebhookEventId,
                square_event_id = parsed.SquareEventId,
                event_type = parsed.EventType,
                object_type = parsed.ObjectType,
                object_id = parsed.ObjectId,
                location_id = parsed.LocationId,
                job_id = jobId,
            }
        );

        var entityKey = parsed.ObjectId ?? parsed.SquareEventId;

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO public.activity_log (activity_id, entity_key, entity_type_key, occurred_at, op_key, payload, priority_key, source, status_key, topic_key)
            VALUES ({activityId}, {entityKey}, {"square_webhook_event"}, now(), {"synced"}, {payloadJson}::jsonb, {"normal"}, {"square.webhook"}, {"info"}, {topicKey});
            """,
            cancellationToken
        );

        logger.LogInformation(
            "MGF.Worker: Square webhook activity logged (event_id={SquareEventId}, type={EventType}, object_id={ObjectId})",
            parsed.SquareEventId,
            parsed.EventType,
            parsed.ObjectId
        );
    }

    private static Task MarkSquareWebhookEventFailedAsync(
        ISquareWebhookStore webhookStore,
        string squareEventId,
        Exception ex,
        CancellationToken cancellationToken
    )
    {
        var errorText = ex.ToString();
        return webhookStore.MarkFailedAsync(squareEventId, errorText, cancellationToken);
    }

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement obj)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            obj = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                break;
            }

            obj = property.Value;
            return true;
        }

        obj = default;
        return false;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }

            if (property.Value.ValueKind == JsonValueKind.Number)
            {
                return property.Value.GetRawText();
            }
        }

        return null;
    }

    private async Task MarkSucceededAsync(
        IJobQueueStore jobQueueStore,
        string jobId,
        CancellationToken cancellationToken)
    {
        await jobQueueStore.MarkSucceededAsync(jobId, cancellationToken);

        logger.LogInformation("MGF.Worker: job {JobId} succeeded", jobId);
    }

    private async Task MarkFailedAsync(
        IJobQueueStore jobQueueStore,
        ClaimedJob job,
        Exception ex,
        CancellationToken cancellationToken)
    {
        var errorText = ex.ToString();
        var newAttemptCount = job.AttemptCount + 1;
        var shouldRetry = newAttemptCount < job.MaxAttempts;

        var delay = shouldRetry ? ComputeBackoffDelay(newAttemptCount) : TimeSpan.Zero;
        var statusKey = shouldRetry ? "queued" : "failed";
        var runAfter = shouldRetry ? DateTimeOffset.UtcNow.Add(delay) : DateTimeOffset.UtcNow;
        DateTimeOffset? finishedAt = shouldRetry ? null : DateTimeOffset.UtcNow;

        await jobQueueStore.MarkFailedAsync(
            new JobFailureUpdate(job.JobId, statusKey, runAfter, finishedAt, errorText),
            cancellationToken);

        if (shouldRetry)
        {
            logger.LogWarning(
                ex,
                "MGF.Worker: job {JobId} failed (attempt {Attempt}/{Max}); requeued for {DelaySeconds:0.0}s",
                job.JobId,
                newAttemptCount,
                job.MaxAttempts,
                delay.TotalSeconds
            );
        }
        else
        {
            logger.LogError(
                ex,
                "MGF.Worker: job {JobId} failed permanently (attempt {Attempt}/{Max})",
                job.JobId,
                newAttemptCount,
                job.MaxAttempts
            );
        }
    }

    private static TimeSpan ComputeBackoffDelay(int attemptCount)
    {
        var seconds = Math.Pow(2, Math.Clamp(attemptCount, 1, 10));
        return TimeSpan.FromSeconds(Math.Min(5 * seconds, 15 * 60));
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

