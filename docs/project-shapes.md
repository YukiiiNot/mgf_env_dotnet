# Project Shapes and Buckets

This document is the canonical source for repo structure conventions. If structure changes, update this page and
`docs/00-index.md` in the same change.

## Bucket layout (src/)
- Core: domain types, contracts, IDs, and rules without IO.
- Application: use-cases and workflow orchestration (primary boundary: `src/Application/MGF.UseCases`).
- Services: runtime hosts (API, Worker) and composition roots.
- Data: persistence, EF model/config, migrations, and stores.
- Integrations: external adapters and API clients.
- Platform: shared runtime infrastructure and reusable engines (e.g., MGF.FolderProvisioning = folder topology provisioning engine for storage providers (Dropbox/NAS/LucidLink)).
- Operations: operational CLIs (migration, bootstrap, delivery); CLI workflows call UseCases, not service hosts.
- DevTools: dev-only utilities and audits (e.g., dev secrets, migrations info, legacy audit).
- Ui: desktop/web UI hosts.

## Naming conventions
- Project folder: `src/<Bucket>/<ProjectName>/`.
- Project name: `MGF.<Area>` or `MGF.<Area>.<SubArea>` (match assembly name).
- Host projects: `MGF.Api`, `MGF.Worker`, `MGF.ProjectBootstrapCli` (bucket reflects host type).
- Test projects: `tests/MGF.<Project>.Tests` or `tests/MGF.<Area>.<SubArea>.Tests`.
- Avoid legacy prefixes or duplicate buckets; prefer moving into the correct bucket.

## Use-case layout (MGF.UseCases)
- Use-cases live under `src/Application/MGF.UseCases/UseCases/<Area>/<UseCaseName>/`.
- Each use-case folder includes `I<UseCaseName>UseCase.cs`, `<UseCaseName>UseCase.cs`, `Models.cs`, and optional `Errors.cs`.

## Data access patterns (Repositories vs Stores)
- Repositories are the default EF-backed access pattern for domain aggregates.
- Stores are narrow, explicit seams for procedural/atomic/JSON patch/bulk operations and for quarantining raw SQL.
- See `docs/persistence-patterns.md` for the detailed checklist.

## Email capability layout
- Platform composition/registry + profile resolution: `src/Platform/MGF.Email`.
- Provider abstractions/models: `src/Core/MGF.Contracts/Abstractions/Email`.
- Provider implementations: `src/Integrations/MGF.Integrations.*` (Dropbox, Email.*, Square).
- Hosts wire selection; do not implement provider logic in services.

## When to create a new project?
Create a new project when you need at least one of:
- A new deployable host boundary (API/Worker/CLI/UI).
- A distinct dependency boundary (heavy or volatile packages isolated from the rest).
- A reusable engine or integration adapter that needs its own lifecycle.
- A clear ownership boundary that cannot be expressed by folders alone.

Do not create a new project just to group files; use folders and namespaces first.

## Slice checklist (structure changes)
- Choose bucket + name using this page.
- Update `MGF.sln` and ensure project references are minimal.
- Ensure required top-level folders exist; add `.gitkeep` for empty skeleton folders.
- Add or update signpost README files outside `/docs` (max 10 lines; link to docs).
- Update `docs/00-index.md` and any affected architecture/onboarding pages.
- Build/test to confirm no behavior change.

## Locked Shape Contracts (src/*)
These are per-project shape contracts enforced by `tests/MGF.Architecture.Tests`. Top-level folders are immediate
child directories under each project root (ignore `bin/` and `obj/`). Required folders must exist in the repo;
use `.gitkeep` when a required folder would otherwise be empty.

Deviation policy: any change to a project's allowed/required/forbidden folders must update this section and the
architecture tests in the same PR.

### MGF.UseCases (`src/Application/MGF.UseCases`)
- Allowed: `UseCases`, `Properties`
- Required: `UseCases`, `Properties`
- Forbidden: `Docs`, `Controllers`, `Services`, `Stores`, `Integrations`
- Rule: All use-cases live under `UseCases/<Area>/<UseCaseName>/`.

### MGF.Contracts (`src/Core/MGF.Contracts`)
- Allowed: `Abstractions`
- Required: `Abstractions`
- Forbidden: `Docs`, `Controllers`, `Services`, `Stores`
- Rule: Contracts/DTOs only; no host or persistence concerns.

### MGF.Domain (`src/Core/MGF.Domain`)
- Allowed: `Entities`
- Required: `Entities`
- Forbidden: `Docs`, `Controllers`, `Services`, `Stores`
- Rule: Domain entities and IDs only.

### MGF.Data (`src/Data/MGF.Data`)
- Allowed: `Configuration`, `Data`, `Migrations`, `Options`, `Stores`, `Properties`
- Required: `Configuration`, `Data`, `Migrations`, `Options`, `Stores`, `Properties`
- Forbidden: `Abstractions`, `Docs`, `Controllers`
- Rule: Repositories in `Data/Repositories`, SQL in `Stores/<Area>`.

### MGF.DataMigrator (`src/Data/MGF.DataMigrator`)
- Allowed: `Commands`
- Required: `Commands`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: CLI host; commands live under `Commands/`.

### MGF.DbMigrationsInfoCli (`src/DevTools/MGF.DbMigrationsInfoCli`)
- Allowed: `Commands`
- Required: `Commands`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: CLI host; commands live under `Commands/`.

### MGF.DevSecretsCli (`src/DevTools/MGF.DevSecretsCli`)
- Allowed: `Commands`, `Models`
- Required: `Commands`, `Models`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: CLI + models only.

### MGF.LegacyAuditCli (`src/DevTools/MGF.LegacyAuditCli`)
- Allowed: `Commands`, `Models`, `Properties`, `Reporting`, `Scanning`
- Required: `Commands`, `Models`, `Properties`, `Reporting`, `Scanning`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Command-driven CLI layout.

### MGF.ProjectBootstrapDevCli (`src/DevTools/MGF.ProjectBootstrapDevCli`)
- Allowed: `Commands`
- Required: `Commands`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: CLI host; commands live under `Commands/`.

### MGF.SquareImportCli (`src/DevTools/MGF.SquareImportCli`)
- Allowed: `Commands`, `Guards`, `Importers`, `Normalization`, `Parsing`, `Properties`, `Reporting`
- Required: `Commands`, `Guards`, `Importers`, `Normalization`, `Parsing`, `Properties`, `Reporting`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Command + pipeline layout; docs live under `/docs`.

### MGF.Integrations.Dropbox (`src/Integrations/MGF.Integrations.Dropbox`)
- Allowed: `Clients`
- Required: `Clients`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Integration client only; keep flat unless a stable folder convention emerges.

### MGF.Integrations.Square (`src/Integrations/MGF.Integrations.Square`)
- Allowed: `Clients`
- Required: `Clients`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Provider implementation only; keep flat.

### MGF.Integrations.Email.Gmail (`src/Integrations/MGF.Integrations.Email.Gmail`)
- Allowed: `Clients`
- Required: `Clients`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Provider implementation only; keep flat.

### MGF.Integrations.Email.Smtp (`src/Integrations/MGF.Integrations.Email.Smtp`)
- Allowed: `Clients`
- Required: `Clients`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Provider implementation only; keep flat.

### MGF.Integrations.Email.Preview (`src/Integrations/MGF.Integrations.Email.Preview`)
- Allowed: `Clients`
- Required: `Clients`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Provider implementation only; writes email previews to disk.

### MGF.ProjectBootstrapCli (`src/Operations/MGF.ProjectBootstrapCli`)
- Allowed: `Commands`, `Properties`
- Required: `Commands`, `Properties`
- Forbidden: `Docs`, `Controllers`, `Services`
- Rule: CLI host; minimal folders.

### MGF.ProvisionerCli (`src/Operations/MGF.ProvisionerCli`)
- Allowed: `Commands`
- Required: `Commands`
- Forbidden: `Docs`, `Controllers`, `Services`
- Rule: CLI host; commands live under `Commands/`.

### MGF.Email (`src/Platform/MGF.Email`)
- Allowed: `Composition`, `Models`, `Registry`
- Required: `Composition`, `Models`, `Registry`
- Forbidden: `Docs`, `Senders`, `Integrations`
- Rule: Composition/registry only; no provider logic.

### MGF.Storage (`src/Platform/MGF.Storage`)
- Allowed: `RootIntegrity`, `Properties`
- Required: `RootIntegrity`, `Properties`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Storage/local filesystem adapters only; keep IO out of hosts and UseCases.

### MGF.FolderProvisioning (`src/Platform/MGF.FolderProvisioning`)
- Allowed: `Provisioning`
- Required: `Provisioning`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Engine + policy under `Provisioning/`.

### MGF.Api (`src/Services/MGF.Api`)
- Allowed: `Controllers`, `Middleware`, `Properties`, `Services`, `Hosting`
- Required: `Controllers`, `Middleware`, `Properties`, `Services`, `Hosting`
- Forbidden: `Docs`, `Stores`
- Rule: HTTP host; adapters + wiring only.

### MGF.Operations.Runtime (`src/Services/MGF.Operations.Runtime`)
- Allowed: `Hosting`
- Required: `Hosting`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Host entrypoint; hosting code lives under `Hosting/`.

### MGF.Worker (`src/Services/MGF.Worker`)
- Allowed: `Properties`, `Hosting`, `Jobs`, `Adapters`
- Required: `Properties`, `Hosting`, `Jobs`, `Adapters`
- Forbidden: `Docs`, `Controllers`
- Rule: Worker host layout; adapters live under `Adapters/Storage`.

### MGF.Desktop (`src/Ui/MGF.Desktop`)
- Allowed: `Hosting`, `Views`, `Properties`
- Required: `Hosting`, `Views`, `Properties`
- Forbidden: `Docs`, `Controllers`, `Stores`
- Rule: UI host; hosting in `Hosting/`, views under `Views/<Surface>/...` with shared styles/assets in `Views/Shared/`.

### MGF.Website (`src/Ui/MGF.Website`)
- Allowed: `Hosting`
- Required: `Hosting`
- Forbidden: `Docs`, `Controllers`, `Stores`
- Rule: UI host stub; hosting bootstrap only until the web surface is defined.

## Known Non-Compliance Backlog (Planned Slices)
- `MGF.Worker`: target shape is host-only with `Jobs/` (migrate each workflow into UseCases, leave only handlers/wiring).

## Architecture guardrails (tests/MGF.Architecture.Tests)
- UseCases and Operations must not depend on Data/EF/Npgsql (exceptions listed in the test file).
- Contracts remain host-agnostic (no Configuration/Options/DI/Hosting).
- No DevTools dependencies from production projects.
- README.md files outside `/docs` are signposts only (<=10 lines, docs links only).

