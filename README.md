# MGF Repository

Purpose  
Provide a signpost overview of the repo and link to canonical documentation.

Audience  
Engineers and operators working in this repo.

Scope  
High-level orientation, system structure, and safe dev quickstart. Does not replace /docs.

Status  
Active

---

## Key Takeaways

- Start with the docs index: [docs/00-index.md](./docs/00-index.md).
- Architecture, bucket boundaries, and dependencies live in [docs/02-architecture/system-overview.md](./docs/02-architecture/system-overview.md) and [docs/02-architecture/project-shapes.md](./docs/02-architecture/project-shapes.md).
- Persistence rules and store patterns are in [docs/02-architecture/persistence-patterns.md](./docs/02-architecture/persistence-patterns.md).
- Shared Dev concurrency and workflow locks are documented in [docs/02-architecture/shared-dev-concurrency.md](./docs/02-architecture/shared-dev-concurrency.md).
- This repo targets .NET `net10.0` (see solution projects in `MGF.sln`).

---

## System Context

MGF is the internal platform for project workflows, delivery, and storage-integrated operations.
The system is organized into strict buckets with directed dependencies and a shared-dev concurrency
model; see the architecture overview in [docs/02-architecture/system-overview.md](./docs/02-architecture/system-overview.md).

---

## Core Concepts

### Architecture Model (Buckets + Dependency Direction)

UseCases are persistence-ignorant, depend on Contracts, and orchestrate workflows.
Data implements persistence, Services host runtimes, Integrations are vendor-only 3rd-party IO,
and Platform contains reusable infrastructure utilities. See [docs/02-architecture/project-shapes.md](./docs/02-architecture/project-shapes.md).

```text
UI / Services / Operations
          |
       UseCases  ---> Contracts <--- Data
          |
     Integrations (vendor-only IO)
          ^
       Platform (shared infra)
```

Related docs: [docs/02-architecture/system-overview.md](./docs/02-architecture/system-overview.md), [docs/02-architecture/application-layer-conventions.md](./docs/02-architecture/application-layer-conventions.md).

## Primary Runtime Components

- API host: `src/Services/MGF.Api` (see [docs/02-architecture/workflows.md](./docs/02-architecture/workflows.md))
- Worker dispatcher: `src/Services/MGF.Worker` (dispatcher only; see [docs/02-architecture/workflows.md](./docs/02-architecture/workflows.md))
- Operations CLIs (call UseCases): `src/Operations/MGF.ProjectBootstrapCli`, `src/Operations/MGF.ProvisionerCli`
- Data migrator: `src/Data/MGF.DataMigrator`

## UI Projects

- Desktop (WPF): `src/Ui/MGF.Desktop`
- Website (future): `src/Ui/MGF.Website`

Related docs: [docs/02-architecture/system-overview.md](./docs/02-architecture/system-overview.md), [docs/02-architecture/project-shapes.md](./docs/02-architecture/project-shapes.md).

## Documentation Map

Canonical docs live under `/docs`. Start with [docs/00-index.md](./docs/00-index.md).

## Dev Quickstart (Safe, Minimal)

For full instructions, see [docs/01-onboarding/getting-started.md](./docs/01-onboarding/getting-started.md).

```bash
dotnet build MGF.sln -c Release
dotnet test MGF.sln -c Release --filter FullyQualifiedName!~MGF.Data.IntegrationTests
```

Before running API/Worker/CLIs, configure dev secrets (see [docs/04-guides/how-to/dev-secrets-tool.md](./docs/04-guides/how-to/dev-secrets-tool.md))
and follow the runbooks and safety rails in [docs/02-architecture/shared-dev-concurrency.md](./docs/02-architecture/shared-dev-concurrency.md).

## Shared Dev Safety Rails

Shared Dev uses workflow locks (`storage.mutation`) and preview email defaults. Always follow
[docs/02-architecture/shared-dev-concurrency.md](./docs/02-architecture/shared-dev-concurrency.md) and
[docs/04-guides/how-to/e2e-email-verification.md](./docs/04-guides/how-to/e2e-email-verification.md).

## Contributing and Ownership

Contributing guidelines are in [CONTRIBUTING.md](./CONTRIBUTING.md). For ownership, use the `Owner`
metadata in the relevant docs under `/docs` and the index at [docs/00-index.md](./docs/00-index.md).

---

## How This Evolves Over Time

- Keep this file a signpost; move canonical guidance to `/docs`.
- Update links when buckets or runbooks change.

---

## Common Pitfalls and Anti-Patterns

- Duplicating documentation outside `/docs`.
- Running storage workflows in shared Dev without checking concurrency rules.

---

## When to Change This Document

- A new bucket or primary runtime component is added.
- Documentation links move or new onboarding flows are introduced.

---

## Related Documents

- [docs/00-index.md](./docs/00-index.md)
- [docs/01-onboarding/getting-started.md](./docs/01-onboarding/getting-started.md)
- [docs/02-architecture/system-overview.md](./docs/02-architecture/system-overview.md)
- [docs/02-architecture/project-shapes.md](./docs/02-architecture/project-shapes.md)
- [docs/02-architecture/persistence-patterns.md](./docs/02-architecture/persistence-patterns.md)
- [docs/02-architecture/shared-dev-concurrency.md](./docs/02-architecture/shared-dev-concurrency.md)

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
