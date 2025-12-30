# Contributing

This repo runs production workflows. Some areas are longâ€‘lived infrastructure contracts and need extra care.

## Protected Infrastructure Areas (Ermano review required)

Changes to the following require review from the infra owner (Ermano):

- `docs/templates/**`
- `docs/schemas/**`
- `src/MGF.Tools.Provisioner/**`
- `src/MGF.Tools.ProjectBootstrap/**`
- `src/MGF.Worker/**`
- Database migrations
- Any code that provisions, validates, repairs, or bootstraps storage

Why: these areas define longâ€‘lived contracts that affect all projects.

## Allowed Without Review (examples)

These are generally ok without infra review:

- API endpoints
- UI / frontend
- Reports
- Nonâ€‘infra domain logic
- Queries
- Tests (unless testing infra)
