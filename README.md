# MGF Repository

Purpose  
Provide a signpost overview of the repo and link to canonical documentation.

Audience  
Engineers and operators working in this repo.

Scope  
High-level orientation, system structure, and safe dev quickstart. Does not replace docs.

Status  
Active

---

## Key Takeaways

- Start with the docs index: 00-index.md.
- Architecture, bucket boundaries, and dependencies live in system-overview.md and project-shapes.md.
- Persistence rules and store patterns are in persistence-patterns.md.
- Shared Dev concurrency and workflow locks are documented in shared-dev-concurrency.md.
- This repo targets .NET `net10.0` (see solution projects in `MGF.sln`).

---

## System Context

MGF is the internal platform for project workflows, delivery, and storage-integrated operations.
The system is organized into strict buckets with directed dependencies and a shared-dev concurrency
model; see the architecture overview in system-overview.md.

---

## Core Concepts

### Architecture Model (Buckets + Dependency Direction)

UseCases are persistence-ignorant, depend on Contracts, and orchestrate workflows.
Data implements persistence, Services host runtimes, Integrations are vendor-only 3rd-party IO,
and Platform contains reusable infrastructure utilities. See project-shapes.md.

```text
UI / Services / Operations
          |
       UseCases  ---> Contracts <--- Data
          |
     Integrations (vendor-only IO)
          ^
       Platform (shared infra)
```

Related docs: system-overview.md, application-layer-conventions.md.

## Primary Runtime Components

- API host: `src/Services/MGF.Api` (see workflows.md)
- Worker dispatcher: `src/Services/MGF.Worker` (dispatcher only; see workflows.md)
- Operations CLIs (call UseCases): `src/Operations/MGF.ProjectBootstrapCli`, `src/Operations/MGF.ProvisionerCli`
- Data migrator: `src/Data/MGF.DataMigrator`

## UI Projects

- Desktop shared (WPF): `src/Ui/MGF.Desktop.Shared`
- Dev Console (WPF): `src/Ui/MGF.DevConsole.Desktop`
- Website (future): `src/Ui/MGF.Website`

Related docs: system-overview.md, project-shapes.md.

## Documentation Map

Canonical docs live under `docs`. Start with 00-index.md.

## Dev Quickstart (Safe, Minimal)

For full instructions, see getting-started.md.

```bash
dotnet build MGF.sln -c Release
dotnet test MGF.sln -c Release --filter FullyQualifiedName!~MGF.Data.IntegrationTests
```

Before running API/Worker/CLIs, configure dev secrets (see dev-secrets-tool.md)
and follow the runbooks and safety rails in shared-dev-concurrency.md.

## Shared Dev Safety Rails

Shared Dev uses workflow locks (`storage.mutation`) and preview email defaults. Always follow
shared-dev-concurrency.md and
e2e-email-verification.md.

## Contributing and Ownership

Contributing guidelines are in CONTRIBUTING.md. For ownership, use the `Owner`
metadata in the relevant docs under `docs` and the index at 00-index.md.

---

## How This Evolves Over Time

- Keep this file a signpost; move canonical guidance to `docs`.
- Update links when buckets or runbooks change.

---

## Common Pitfalls and Anti-Patterns

- Duplicating documentation outside `docs`.
- Running storage workflows in shared Dev without checking concurrency rules.

---

## When to Change This Document

- A new bucket or primary runtime component is added.
- Documentation links move or new onboarding flows are introduced.

---

## Related Documents

- 00-index.md
- getting-started.md
- system-overview.md
- project-shapes.md
- persistence-patterns.md
- shared-dev-concurrency.md

---

## Appendix (Optional)

No appendix content.

---

## Metadata

Last updated: 2026-01-02  
Owner: Documentation  
Review cadence: quarterly  

Change log:
- 2026-01-02 - Initial root README signpost.
