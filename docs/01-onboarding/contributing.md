# Contributing

This repo runs production workflows. Some areas are long-lived infrastructure contracts and need extra care.

## Protected Infrastructure Areas (Ermano review required)

Changes to the following require review from the infra owner (Ermano):

- `docs/templates/**`
- `docs/schemas/**`
- `src/Operations/MGF.Tools.Provisioner/**`
- `src/MGF.Tools.ProjectBootstrap/**`
- `src/Services/MGF.Worker/**`
- Database migrations
- Any code that provisions, validates, repairs, or bootstraps storage

Why: these areas define long-lived contracts that affect all projects.

## Allowed Without Review (examples)

These are generally ok without infra review:

- API endpoints
- UI / frontend
- Reports
- Non-infra domain logic
- Queries
- Tests (unless testing infra)
