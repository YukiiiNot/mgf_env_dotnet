# Core, Application, Services Conventions

This doc defines where workflow code lives and how runtime hosts should depend on it.

## Rule of ownership
- Services host workflows; Application owns the workflows; Data owns persistence; Integrations own external adapters.
- Raw SQL lives in `src/Data/` (or the migrator); job queue SQL lives in `src/Data/MGF.Data/Stores/Jobs`,
  counters live in `src/Data/MGF.Data/Stores/Counters`, delivery persistence lives in `src/Data/MGF.Data/Stores/Delivery`.
  Services/Operations should call Data interfaces instead.

## Scope definitions
- Core (`src/Core/`): domain types, contracts, IDs, and shared rules that have no IO.
- Application (`src/Application/`): use-cases and workflow implementations that orchestrate domain logic. Primary boundary project: `src/Application/MGF.UseCases`.
- Services (`src/Services/`): runtime hosts (API, Worker) that call Application and wire dependencies.
- Data (`src/Data/`): persistence, EF model/config, migrations, and seeding.
- Integrations (`src/Integrations/`): external adapters (Square, Dropbox, email providers).

## Examples (current state)
- `src/Services/MGF.Api`: HTTP host; controllers and middleware are service concerns today.
- `src/Services/MGF.Worker`: job runner; many workflow handlers currently live in Worker.
- `src/Operations/MGF.ProjectBootstrapCli`: CLI orchestrator for provisioning/delivery flows.
- `src/Core/MGF.Contracts`: current home for application abstractions and shared workflow helpers.
- `src/Core/MGF.Domain`: domain entities and IDs.
- `src/Application/MGF.UseCases`: workflow/use-case boundary (scaffold; logic moves here over time).

## Future direction
- New workflows and orchestration logic go in `src/Application/MGF.UseCases`; Services and Operations call into it.
- Services stay thin: hosting, composition root, and transport concerns only.
- Data stays the single owner of persistence and migrations.
- Integrations isolate external APIs and adapters behind interfaces.

## Use-case boundary (MGF.UseCases)
MGF.UseCases is the boundary project for business use-cases and workflows; all business writes flow through use-cases.
Delivery email send now lives in `MGF.UseCases` via `ISendDeliveryEmailUseCase` and is called by `MGF.Worker`.

Examples that belong here:
- CreateProject
- CreateDeliveryVersion
- SendDeliveryEmail

Does not belong here: DbContext, Dropbox SDK, SMTP client.

## Checklist: When adding a new feature, place code in
- If it defines domain types or contracts, put it in `src/Core/`.
- If it implements a workflow or use-case, put it in `src/Application/MGF.UseCases`.
- If it hosts HTTP endpoints or background loops, put it in `src/Services/`.
- If it is a CLI or ops runner, put it in `src/Operations/`.
- If it persists data or defines schema, put it in `src/Data/`.
- If it integrates external APIs, put it in `src/Integrations/`.
- If it is shared runtime glue (config/logging), put it in `src/Platform/`.
- If it is UI, put it in `src/Ui/`.
- If it is dev-only tooling, put it in `src/DevTools/`.
- If unsure, start in `src/Application/` and keep Services thin.
