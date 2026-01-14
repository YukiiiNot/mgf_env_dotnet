# Repo Workflow

> Runbook for repository operations, CI, and migration gating.

---

## MetaData

**Purpose:** Provide the operational workflow for repo branching, CI, and database migrations.
**Scope:** Covers branch model, CI rules, staging/prod migration workflows, and safety rules. Excludes system design rationale.
**Doc Type:** Runbook
**Status:** Active
**Last Updated:** 2026-01-10

---

## TL;DR

- Branch model: feature -> staging -> main.
- CI builds in Release and checks EF drift.
- Staging and prod migrations run via GitHub Actions with approvals.

---

## Main Content

Source of truth: .github workflows, MGF.Data migrations, MGF.DataMigrator

This repo uses EF Core migrations for Postgres schema changes, and GitHub Actions plus GitHub Environments to gate staging/prod database migrations.

## Branch model

- main = production (only updated via PR from staging)
- staging = pre-prod testing (pushes trigger staging DB migrations via approvals)
- feature/**, fix/**, chore/** = short-lived dev branches (PR into staging)

Migrations are created locally, reviewed in PRs, validated by CI, applied automatically to staging with approval, and promoted to production only via main.

## Local dev setup (lead dev machine)

### 1) Set local dev config (recommended)

Follow dev-secrets.md to populate `config/appsettings.Development.json`.

### 2) Required env vars for local dev

```powershell
$env:MGF_ENV = "Dev"
$env:MGF_DB_MODE = "direct"
```

### 3) Apply migrations and seed lookups (Dev)

```powershell
dotnet run --project src/Data/MGF.DataMigrator
```

Do not run this against staging or prod. Staging/prod migrations are applied only via GitHub Actions using EF bundles.

### 4) Run API and Worker locally

```powershell
# API (expects X-MGF-API-KEY header on /api/*)
dotnet run --project src/Services/MGF.Api

# Worker (polls public.jobs)
dotnet run --project src/Services/MGF.Worker
```

### 5) Project provisioning and delivery (jobs)

Project creation is draft-first and provisioning and delivery are explicit jobs:

- project.bootstrap provisions domain roots and project containers.
- project.delivery copies deliverables from LucidLink 02_Renders/Final_Masters into Dropbox delivery containers and writes delivery manifests.

```powershell
# Bootstrap (project + roots)
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- ready --projectId <PROJECT_ID>
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- enqueue `
  --projectId <PROJECT_ID> `
  --editors TE `
  --verifyDomainRoots true `
  --createDomainRoots true `
  --provisionProjectContainers true

# Delivery (see delivery.md for full flow)
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- to-deliver --projectId <PROJECT_ID>
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- deliver --projectId <PROJECT_ID> --editorInitials TE
```

## Editorial file naming and templates

We follow Adobe Premiere Productions norms. The system does not version or merge .prproj files.

- .prproj filenames are stable:
  - MASTER: {PROJECT_CODE}_{PROJECT_NAME}_MASTER.prproj
  - Editor working file: {PROJECT_CODE}_{PROJECT_NAME}_{EDITOR_INITIALS}.prproj
- Sequence naming (inside Premiere) can carry versions.
- Exports and renders use versioning (e.g., ..._EXPORT_v###.mp4).

Templates and schemas:
- Folder templates live in artifacts/templates/
  - Domain roots: domain_dropbox_root.json, domain_lucidlink_root.json, domain_nas_root.json
  - Project containers: dropbox_project_container.json, lucidlink_production_container.json, nas_archive_container.json
  - Delivery container: dropbox_delivery_container.json
- JSON Schemas live in artifacts/schemas/
  - mgf.folderTemplate.schema.json
  - mgf.namingRules.schema.json

All templates include:
- 99_Dump/ at top level
- 00_Admin\.mgf\manifest\folder_manifest.json

## Creating migrations (local)

Use EF CLI with:
- migrations project: src/Data/MGF.Data
- startup project: src/Data/MGF.DataMigrator

```powershell
dotnet tool restore
dotnet ef migrations add <Name> --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator
```

Reminder: commit the generated migration files under src/Data/MGF.Data/Migrations/.

## Check EF model drift locally

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator -c Release
```

## CI rules

Workflow: .github/workflows/ci.yml

CI runs on:
- PRs targeting main or staging
- pushes to main, staging, and feature/**, fix/**, chore/**

CI checks:
- restore and build in Release (includes the WPF project; Windows runner required)
- unit tests only:
  - tests/MGF.Domain.Tests/MGF.Domain.Tests.csproj
  - tests/MGF.Contracts.Tests/MGF.Contracts.Tests.csproj
- EF drift check: dotnet ef migrations has-pending-model-changes ...
- guardrail scan for destructive SQL keywords in non-test code

CI does not run DB integration tests.

## Deploying to Staging

Workflow: .github/workflows/migrate-staging.yml

Trigger:
- push to staging (workflow job is gated by GitHub Environment approvals)

Required GitHub Environment:
- environment name: staging
- secret: STAGING_DB_DIRECT_CONNECTION_STRING

What it does:
- builds in Release
- prints applied migrations (__EFMigrationsHistory) before and after
- builds an EF migrations bundle and applies migrations to the staging DB (migrations only; does not run the migrator seeding logic)

## Deploying to Production

Workflow: .github/workflows/migrate-prod.yml

Trigger:
- push to main (intended via PR merge from staging)

Required GitHub Environment:
- environment name: prod
- secret: PROD_DB_DIRECT_CONNECTION_STRING

What it does:
- builds in Release
- prints applied migrations (__EFMigrationsHistory) before and after
- builds an EF migrations bundle and applies migrations to the prod DB (migrations only; does not run the migrator seeding logic)

## Safety rules

- Staging/prod DB credentials must only live in GitHub Environment secrets (never in git, never in shared docs or chat logs).
- Do not run integration tests against shared DBs: tests/MGF.Data.IntegrationTests truncate core tables by design.
- Destructive flags are for local Dev DB only when intentionally resetting data:
  - MGF_ALLOW_DESTRUCTIVE=true
  - MGF_DESTRUCTIVE_ACK=I_UNDERSTAND

## Quick command cheat sheet

```powershell
# build
dotnet build MGF.sln -c Release

# unit tests only
dotnet test tests/MGF.Domain.Tests/MGF.Domain.Tests.csproj -c Release
dotnet test tests/MGF.Contracts.Tests/MGF.Contracts.Tests.csproj -c Release

# add migration
dotnet tool restore
dotnet ef migrations add <Name> --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator

# drift check
dotnet ef migrations has-pending-model-changes --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator -c Release

# generate migrations bundle locally (optional)
dotnet ef migrations bundle --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator -c Release --output runtime/efbundle-local.exe

# branch flow example
git checkout -b feature/my-change
git push -u origin feature/my-change

# after PR merge to staging, deploy staging by pushing staging
git checkout staging
git pull
git push

# deploy prod by PR merging staging -> main (then push or merge triggers migrate-prod.yml)
```

---

## System Context

This runbook governs operational workflows for repo branches, CI, and database migration gating.

---

## Core Concepts

- The branch model enforces staged promotion from feature to staging to main.
- CI validates schema drift and guardrails before promotion.

---

## How This Evolves Over Time

- Update when branching policy, CI checks, or migration automation changes.
- Add new safety rules when destructive behaviors are introduced.

---

## Common Pitfalls and Anti-Patterns

- Running migrations locally against staging or prod.
- Skipping EF drift checks before promotion.

---

## When to Change This Document

- Branching policy, CI workflows, or migration automation changes.

---

## Related Documents
- db-migrations.md
- destructive-ops-audit.md
- delivery.md

## Change Log
- 2026-01-10 - Updated local dev setup to repo-root config file workflow.
