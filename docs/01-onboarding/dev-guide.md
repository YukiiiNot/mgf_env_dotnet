# Developer Guide (MGF)

This repo favors guardrails, contracts, and runbooks over cleverness.

## Conventions

- Prefer explicit jobs + status transitions over implicit side effects.
- Every workflow should have:
  - explicit job type
  - idempotent behavior
  - contract tests
  - a runbook (docs/)
- Persistence patterns (repos vs stores) live in [../persistence-patterns.md](../persistence-patterns.md).
- Structure and naming conventions live in [../project-shapes.md](../project-shapes.md).

## Integrations

Add new integrations under `src/Integrations/MGF.Integrations.<Provider>/`.
Keep APIs behind small interfaces so tests can fake them, with Worker calling the integration.

## Provisioning engine

MGF.FolderProvisioning is the reusable provisioning engine; MGF-specific rules live in
`src/Platform/MGF.FolderProvisioning/Provisioning/Policy`. See [../02-architecture/provisioning.md](../02-architecture/provisioning.md).

## Email subsystem

Email composition/registry lives under `src/Platform/MGF.Email/`. Provider senders live under
`src/Integrations/MGF.Integrations.Email.*` and implement abstractions in
`src/Core/MGF.Contracts/Abstractions/Email/`. Worker wires selection at runtime.
Add new emails by:
1. Create context model + composer
2. Add templates (.html/.txt) under `src/Platform/MGF.Email/Composition/Templates/`
3. Register composer in the registry
4. Add/extend tests and preview fixtures

## Testing philosophy

- Unit tests for builders/planners
- Contract tests for policies (e.g., allowlist, stable share path)
- Avoid destructive DB tests unless explicitly opt-in

## Use-case boundary (MGF.UseCases)

MGF.UseCases is the boundary project for business workflows. See [../project-shapes.md](../project-shapes.md) for
project placement and ownership rules.

## Where to add new workflows

- Use-cases: `src/Application/MGF.UseCases`
- Job handlers: `src/Services/MGF.Worker/<Workflow>`
- CLI commands: `src/Operations/MGF.ProjectBootstrapCli/Program.cs` (call UseCases, not hosts)
- Runbooks: `docs/05-runbooks/`
- Templates/contracts: `artifacts/templates/` and `docs/03-contracts/storage/infra-contracts.md`

