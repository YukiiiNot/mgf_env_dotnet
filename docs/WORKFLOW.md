# Repo workflow runbook (MGF)

This repo uses EF Core migrations for Postgres schema changes, and GitHub Actions + GitHub Environments to gate staging/prod database migrations.

## Branch model

- `main` = **production** (sacred; only updated via PR from `staging`)
- `staging` = **pre-prod testing** (pushes trigger staging DB migrations via approvals)
- `feature/**`, `fix/**`, `chore/**` = short-lived dev branches (PR into `staging`)

Migrations are created locally, reviewed in PRs, validated by CI, applied automatically to staging with approval, and promoted to production only via main.

## Local dev setup (lead dev machine)

### 1) Set Dev DB user-secrets (recommended)

Secrets are stored under the `MGF.Infrastructure` project.

```powershell
cd C:\dev\mgf_env_dotnet

# Dev DB (direct host recommended for migrations)
dotnet user-secrets set "Database:Dev:DirectConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure

# Optional: app/runtime API key (needed to call /api/* endpoints)
dotnet user-secrets set "Security:ApiKey" "<long random string>" --project src/MGF.Infrastructure

```

### 2) Required env vars for local dev


```powershell
$env:MGF_ENV = "Dev"
$env:MGF_DB_MODE = "direct"
```

### 3) Apply migrations + seed lookups (Dev)

```powershell
dotnet run --project src/MGF.Tools.Migrator
```

⚠️ Do not run this against staging or prod.
Staging/prod migrations are applied only via GitHub Actions using EF bundles.

### 4) Run API / Worker locally

```powershell
# API (expects X-MGF-API-KEY header on /api/*)
dotnet run --project src/MGF.Api

# Worker (polls public.jobs)
dotnet run --project src/MGF.Worker
```

### 5) Project provisioning + delivery (jobs)

Project creation is **draft-first** and provisioning/delivery are **explicit** jobs:

- `project.bootstrap` provisions domain roots + project containers.
- `project.delivery` copies deliverables from LucidLink `02_Renders/Final_Masters` into Dropbox delivery containers and writes delivery manifests.

```powershell
# Bootstrap (project + roots)
dotnet run --project src/MGF.Tools.ProjectBootstrap -- ready --projectId <PROJECT_ID>
dotnet run --project src/MGF.Tools.ProjectBootstrap -- enqueue `
  --projectId <PROJECT_ID> `
  --editors TE `
  --verifyDomainRoots true `
  --createDomainRoots true `
  --provisionProjectContainers true

# Delivery (see docs/RUNBOOK_DELIVERY.md for full flow)
dotnet run --project src/MGF.Tools.ProjectBootstrap -- to-deliver --projectId <PROJECT_ID>
dotnet run --project src/MGF.Tools.ProjectBootstrap -- deliver --projectId <PROJECT_ID> --editorInitials TE
```

## Editorial file naming + templates

We follow Adobe Premiere Productions norms. The system **does not** version or merge `.prproj` files.

- `.prproj` filenames are **stable**:
  - MASTER: `{PROJECT_CODE}_{PROJECT_NAME}_MASTER.prproj`
  - Editor working file: `{PROJECT_CODE}_{PROJECT_NAME}_{EDITOR_INITIALS}.prproj`
- Sequence naming (inside Premiere) can carry versions.
- Exports/renders **do** use versioning (e.g., `..._EXPORT_v###.mp4`).

Templates and schemas:
- Folder templates live in `docs/templates/`
  - Domain roots: `domain_dropbox_root.json`, `domain_lucidlink_root.json`, `domain_nas_root.json`
  - Project containers: `dropbox_project_container.json`, `lucidlink_production_container.json`, `nas_archive_container.json`
  - Delivery container: `dropbox_delivery_container.json`
- JSON Schemas live in `docs/schemas/`
  - `mgf.folderTemplate.schema.json`
  - `mgf.namingRules.schema.json`

All templates include:
- `99_Dump/` at top level
- `00_Admin\.mgf\manifest\folder_manifest.json`

## Creating migrations (local)

Use EF CLI with:
- migrations project: `src/MGF.Infrastructure`
- startup project: `src/MGF.Tools.Migrator`

```powershell
dotnet tool restore
dotnet ef migrations add <Name> --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
```

Reminder: commit the generated migration files under `src/MGF.Infrastructure/Migrations/`.

## Check EF model drift locally

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator -c Release
```

## CI rules

Workflow: `.github/workflows/ci.yml`

CI runs on:
- PRs targeting `main` or `staging`
- pushes to `main`, `staging`, and `feature/**`, `fix/**`, `chore/**`

CI checks:
- restore + build in `Release` (includes the WPF project; Windows runner required)
- unit tests only:
  - `tests/MGF.Domain.Tests/MGF.Domain.Tests.csproj`
  - `tests/MGF.Application.Tests/MGF.Application.Tests.csproj`
- EF drift check: `dotnet ef migrations has-pending-model-changes ...`
- guardrail scan for destructive SQL keywords in non-test code

CI does **not** run DB integration tests.

## Deploying to Staging

Workflow: `.github/workflows/migrate-staging.yml`

Trigger:
- push to `staging` (workflow job is gated by GitHub Environment approvals)

Required GitHub Environment:
- environment name: `staging`
- secret: `STAGING_DB_DIRECT_CONNECTION_STRING`

What it does:
- builds in `Release`
- prints applied migrations (`__EFMigrationsHistory`) before/after
- builds an EF migrations bundle and applies migrations to the staging DB (migrations only; does not run the migrator seeding logic)

## Deploying to Production

Workflow: `.github/workflows/migrate-prod.yml`

Trigger:
- push to `main` (intended via PR merge from `staging`)

Required GitHub Environment:
- environment name: `prod`
- secret: `PROD_DB_DIRECT_CONNECTION_STRING`

What it does:
- builds in `Release`
- prints applied migrations (`__EFMigrationsHistory`) before/after
- builds an EF migrations bundle and applies migrations to the prod DB (migrations only; does not run the migrator seeding logic)

## Safety rules

- Staging/prod DB credentials must only live in GitHub Environment secrets (never in git, never in shared docs/chat logs).
- Don't run integration tests against shared DBs: `tests/MGF.Infrastructure.IntegrationTests` **TRUNCATE** core tables by design.
- Destructive flags are for local Dev DB only when intentionally resetting data:
  - `MGF_ALLOW_DESTRUCTIVE=true`
  - `MGF_DESTRUCTIVE_ACK=I_UNDERSTAND`

## Quick command cheat sheet

```powershell
# build
dotnet build .\MGF.sln -c Release

# unit tests only
dotnet test .\tests\MGF.Domain.Tests\MGF.Domain.Tests.csproj -c Release
dotnet test .\tests\MGF.Application.Tests\MGF.Application.Tests.csproj -c Release

# add migration
dotnet tool restore
dotnet ef migrations add <Name> --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator

# drift check
dotnet ef migrations has-pending-model-changes --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator -c Release

# generate migrations bundle locally (optional)
dotnet ef migrations bundle --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator -c Release --output .\runtime\efbundle-local.exe

# branch flow example
git checkout -b feature/my-change
git push -u origin feature/my-change

# (after PR merge to staging) deploy staging by pushing staging
git checkout staging
git pull
git push

# deploy prod by PR merging staging -> main (then push/merge triggers migrate-prod.yml)
```
