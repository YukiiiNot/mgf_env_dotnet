# Developer Guide (MGF)

This repo favors guardrails, contracts, and runbooks over cleverness.

## Conventions

- Prefer explicit jobs + status transitions over implicit side effects.
- Every workflow should have:
  - explicit job type
  - idempotent behavior
  - contract tests
  - a runbook (docs/)
- Raw SQL belongs in `src/Data/` (or migrator projects), never in Services or Operations.
  Job queue SQL lives in `src/Data/MGF.Data/Stores/Jobs`, counters live in `src/Data/MGF.Data/Stores/Counters`,
  delivery persistence lives in `src/Data/MGF.Data/Stores/Delivery`, and project bootstrap persistence lives in
  `src/Data/MGF.Data/Stores/ProjectBootstrap` (Worker bootstrapper must not run SQL directly).
- Pattern: define a Data interface (e.g., `ISquareWebhookStore`, `IJobQueueStore`, `ICounterAllocator`, `IProjectDeliveryStore`,
  `IProjectBootstrapStore`) and inject it into hosts.

## Integrations

Add new integrations under `src/Services/MGF.Worker/Integrations/<Provider>`.
Keep APIs behind small interfaces so tests can fake them.

## Provisioning engine

MGF.Provisioning is the reusable provisioning engine; MGF-specific rules live in
`src/Platform/MGF.Provisioning/Provisioning/Policy`. See [../02-architecture/provisioning.md](../02-architecture/provisioning.md).

## Email subsystem

All email work lives under `src/Services/MGF.Worker/Email/`.
Add new emails by:
1. Create context model + composer
2. Add templates (.html/.txt)
3. Register composer in the registry
4. Add/extend tests and preview fixtures

## Testing philosophy

- Unit tests for builders/planners
- Contract tests for policies (e.g., allowlist, stable share path)
- Avoid destructive DB tests unless explicitly opt-in

## Use-case boundary (MGF.UseCases)

MGF.UseCases is the boundary project for business use-cases and workflows; all business writes flow through use-cases.
Delivery email send now flows through `ISendDeliveryEmailUseCase` from `MGF.Worker`.

Examples that belong here:
- CreateProject
- CreateDeliveryVersion
- SendDeliveryEmail

Does not belong here: DbContext, Dropbox SDK, SMTP client.

## Where to add new workflows

- Use-cases: `src/Application/MGF.UseCases`
- Job handlers: `src/Services/MGF.Worker/<Workflow>`
- CLI commands: `src/Operations/MGF.ProjectBootstrapCli/Program.cs`
- Runbooks: `docs/05-runbooks/`
- Templates/contracts: `artifacts/templates/` and `docs/03-contracts/storage/infra-contracts.md`
