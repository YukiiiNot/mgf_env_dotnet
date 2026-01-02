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
- Add or update signpost README files outside `/docs` (max 10 lines; link to docs).
- Update `docs/00-index.md` and any affected architecture/onboarding pages.
- Build/test to confirm no behavior change.

## Locked Shape Contracts (src/*)
These are per-project shape contracts enforced by `tests/MGF.Architecture.Tests`. Top-level folders are immediate
child directories under each project root (ignore `bin/` and `obj/`).

Deviation policy: any change to a project's allowed/required/forbidden folders must update this section and the
architecture tests in the same PR.

### MGF.UseCases (`src/Application/MGF.UseCases`)
- Allowed: `UseCases`
- Required: `UseCases`
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
- Allowed: `Configuration`, `Data`, `Migrations`, `Options`, `Stores`
- Required: `Data`, `Stores`
- Forbidden: `Abstractions`, `Docs`, `Controllers`
- Rule: Repositories in `Data/Repositories`, SQL in `Stores/<Area>`.

### MGF.DataMigrator (`src/Data/MGF.DataMigrator`)
- Allowed: *(none)*
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: CLI host; keep flat.

### MGF.DbMigrationsInfoCli (`src/DevTools/MGF.DbMigrationsInfoCli`)
- Allowed: *(none)*
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: CLI host; keep flat.

### MGF.DevSecretsCli (`src/DevTools/MGF.DevSecretsCli`)
- Allowed: `Models`
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: CLI + models only.

### MGF.LegacyAuditCli (`src/DevTools/MGF.LegacyAuditCli`)
- Allowed: `Commands`, `Models`, `Properties`, `Reporting`, `Scanning`
- Required: `Commands`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Command-driven CLI layout.

### MGF.ProjectBootstrapDevCli (`src/DevTools/MGF.ProjectBootstrapDevCli`)
- Allowed: *(none)*
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: CLI host; keep flat.

### MGF.SquareImportCli (`src/DevTools/MGF.SquareImportCli`)
- Allowed: `Commands`, `Guards`, `Importers`, `Normalization`, `Parsing`, `Properties`, `Reporting`
- Required: `Commands`, `Importers`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Command + pipeline layout; docs live under `/docs`.

### MGF.Integrations.Dropbox (`src/Integrations/MGF.Integrations.Dropbox`)
- Allowed: *(none)*
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Integration client only; keep flat unless a stable folder convention emerges.

### MGF.Integrations.Square (`src/Integrations/MGF.Integrations.Square`)
- Allowed: *(none)*
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Provider implementation only; keep flat.

### MGF.Integrations.Email.Gmail (`src/Integrations/MGF.Integrations.Email.Gmail`)
- Allowed: *(none)*
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Provider implementation only; keep flat.

### MGF.Integrations.Email.Smtp (`src/Integrations/MGF.Integrations.Email.Smtp`)
- Allowed: *(none)*
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Provider implementation only; keep flat.

### MGF.ProjectBootstrapCli (`src/Operations/MGF.ProjectBootstrapCli`)
- Allowed: `Properties`
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `Services`
- Rule: CLI host; minimal folders.

### MGF.ProvisionerCli (`src/Operations/MGF.ProvisionerCli`)
- Allowed: *(none)*
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `Services`
- Rule: CLI host; keep flat.

### MGF.Email (`src/Platform/MGF.Email`)
- Allowed: `Composition`, `Models`, `Registry`
- Required: `Composition`
- Forbidden: `Docs`, `Senders`, `Integrations`
- Rule: Composition/registry only; no provider logic.

### MGF.Storage (`src/Platform/MGF.Storage`)
- Allowed: `RootIntegrity`
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Storage/local filesystem adapters only; keep IO out of hosts and UseCases.

### MGF.FolderProvisioning (`src/Platform/MGF.FolderProvisioning`)
- Allowed: `Provisioning`
- Required: `Provisioning`
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Engine + policy under `Provisioning/`.

### MGF.Api (`src/Services/MGF.Api`)
- Allowed: `Controllers`, `Middleware`, `Properties`, `Services`
- Required: `Controllers`
- Forbidden: `Docs`, `Stores`
- Rule: HTTP host; adapters + wiring only.

### MGF.Operations.Runtime (`src/Services/MGF.Operations.Runtime`)
- Allowed: *(none)*
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Host entrypoint; keep flat.

### MGF.Worker.Adapters.Storage (`src/Services/MGF.Worker.Adapters.Storage`)
- Allowed: `ProjectBootstrap`, `ProjectDelivery`
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `UseCases`
- Rule: Host adapter for storage/provisioning IO; implements Contracts gateways for Worker.

### MGF.Worker (`src/Services/MGF.Worker`)
- Allowed: `ProjectArchive`, `Properties`
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`
- Rule: Worker host layout (current shape); workflow folders are temporary (see backlog).

### MGF.Desktop.Wpf (`src/Ui/MGF.Desktop.Wpf`)
- Allowed: *(none)*
- Required: *(none)*
- Forbidden: `Docs`, `Controllers`, `Stores`
- Rule: UI host; keep flat.

## Known Non-Compliance Backlog (Planned Slices)
- `MGF.Worker`: currently contains `ProjectArchive`; target shape is host-only with `Jobs/` (migrate each workflow into UseCases, leave only handlers/wiring).

## Architecture guardrails (tests/MGF.Architecture.Tests)
- UseCases and Operations must not depend on Data/EF/Npgsql (exceptions listed in the test file).
- Contracts remain host-agnostic (no Configuration/Options/DI/Hosting).
- No DevTools dependencies from production projects.
- README.md files outside `/docs` are signposts only (<=10 lines, docs links only).

