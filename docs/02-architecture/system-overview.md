# System Overview

This repo hosts MGF's internal API, worker, desktop app, and supporting tools for storage provisioning, migrations, and delivery workflows.

## Components
- `src/Services/MGF.Api` - internal API entrypoint for apps and integrations.
- `src/Services/MGF.Worker` - background job processor for provisioning, delivery, and integrations.
- `src/Ui/MGF.Desktop.Wpf` - desktop ops console (early stage).
- `src/DevTools/*` and `src/Operations/*` - CLIs for migrations, provisioning, delivery, and audits.
- `src/Platform/MGF.Email` - email composition/registry used by Worker and ops tools (templates copied by hosts).
- `src/Platform/MGF.FolderProvisioning` - provisioning engine (template planning/execution) with replaceable policy rules.
- `src/Platform/MGF.Storage` - storage/local filesystem adapters (RootIntegrity executor).
- `src/Data/MGF.Data` - shared data access, configuration, and EF model.
- `src/Integrations/MGF.Integrations.Email.*` - provider-specific email senders (Gmail, SMTP).
- `src/Integrations/MGF.Integrations.*` - external API adapters (Dropbox, etc.).
- `src/Application/MGF.UseCases` - use-case boundary for business workflows.

## Runtime flow
Worker/API/CLI -> UseCases -> Contracts -> Data/Integrations (CLIs call UseCases; do not reference service hosts).
Project bootstrap: Worker -> `IBootstrapProjectUseCase` -> Contracts store -> Data + `IProjectBootstrapProvisioningGateway` (Services adapter: MGF.Worker.Adapters.Storage).
Square webhooks: API -> `IIngestSquareWebhookUseCase` -> Contracts store -> Data.
People list: API -> `IListPeopleUseCase` -> Contracts store -> Data.

## Use-case boundary (MGF.UseCases)
MGF.UseCases is the boundary project for business use-cases and workflows; all business writes flow through use-cases.

Examples that belong here:
- CreateProject
- CreateDeliveryVersion
- SendDeliveryEmail

Does not belong here: DbContext, Dropbox SDK, SMTP client.

## Related docs
- Workflow overview: [workflows.md](workflows.md)
- Provisioning engine: [provisioning.md](provisioning.md)
- Project shapes: [../project-shapes.md](../project-shapes.md)
- Persistence patterns: [../persistence-patterns.md](../persistence-patterns.md)
- Runbooks: [../05-runbooks/repo-workflow.md](../05-runbooks/repo-workflow.md)
- Contracts index: [../03-contracts/](../03-contracts/)



