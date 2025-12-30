# Developer Guide (MGF)

This repo favors guardrails, contracts, and runbooks over cleverness.

## Conventions

- Prefer explicit jobs + status transitions over implicit side effects.
- Every workflow should have:
  - explicit job type
  - idempotent behavior
  - contract tests
  - a runbook (docs/)

## Integrations

Add new integrations under `src/MGF.Worker/Integrations/<Provider>`.
Keep APIs behind small interfaces so tests can fake them.

## Email subsystem

All email work lives under `src/MGF.Worker/Email/`.
Add new emails by:
1. Create context model + composer
2. Add templates (.html/.txt)
3. Register composer in the registry
4. Add/extend tests and preview fixtures

## Testing philosophy

- Unit tests for builders/planners
- Contract tests for policies (e.g., allowlist, stable share path)
- Avoid destructive DB tests unless explicitly opt-in

## Where to add new workflows

- Job handlers: `src/MGF.Worker/<Workflow>`
- CLI commands: `src/MGF.Tools.ProjectBootstrap/Program.cs`
- Runbooks: `docs/05-runbooks/`
- Templates/contracts: `docs/templates/` and `docs/03-contracts/storage/infra-contracts.md`
