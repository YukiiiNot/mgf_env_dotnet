# Core, Application, Services Conventions

This doc defines where workflow code lives and how runtime hosts should depend on it.

## Rule of ownership
- Services host workflows; Application owns the workflows; Data owns persistence; Integrations own external adapters.

## Scope definitions
- Core (`src/Core/`): domain types, contracts, IDs, and shared rules that have no IO.
- Application (`src/Application/`): use-cases and workflow implementations that orchestrate domain logic.
- Services (`src/Services/`): runtime hosts (API, Worker) that call Application and wire dependencies.
- Data (`src/Data/`): persistence, EF model/config, migrations, and seeding.
- Integrations (`src/Integrations/`): external adapters (Square, Dropbox, email providers).

## Examples (current state)
- `src/Services/MGF.Api`: HTTP host; controllers and middleware are service concerns today.
- `src/Services/MGF.Worker`: job runner; many workflow handlers currently live in Worker.
- `src/Operations/MGF.ProjectBootstrapCli`: CLI orchestrator for provisioning/delivery flows.
- `src/Core/MGF.Application`: current home for application abstractions and shared workflow helpers.
- `src/Core/MGF.Domain`: domain entities and IDs.

## Future direction
- New workflows and orchestration logic go in `src/Application/`; Services and Operations call into it.
- Services stay thin: hosting, composition root, and transport concerns only.
- Data stays the single owner of persistence and migrations.
- Integrations isolate external APIs and adapters behind interfaces.

## Checklist: When adding a new feature, place code in
- If it defines domain types or contracts, put it in `src/Core/`.
- If it implements a workflow or use-case, put it in `src/Application/`.
- If it hosts HTTP endpoints or background loops, put it in `src/Services/`.
- If it is a CLI or ops runner, put it in `src/Operations/`.
- If it persists data or defines schema, put it in `src/Data/`.
- If it integrates external APIs, put it in `src/Integrations/`.
- If it is shared runtime glue (config/logging), put it in `src/Platform/`.
- If it is UI, put it in `src/Ui/`.
- If it is dev-only tooling, put it in `src/DevTools/`.
- If unsure, start in `src/Application/` and keep Services thin.
