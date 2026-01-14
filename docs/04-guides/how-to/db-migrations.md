# Database Workflow (Supabase + EF Core)

> How-to for creating and applying EF Core migrations in MGF.

---

## MetaData

**Purpose:** Provide the supported workflow for creating and applying EF Core migrations.
**Scope:** Covers configuration, migration commands, and CI usage. Excludes schema design rationale.
**Doc Type:** How-To
**Status:** Active
**Last Updated:** 2026-01-10

---

## TL;DR

- EF Core migrations are the source of truth for schema changes.
- Use MGF.DataMigrator to apply migrations.
- Do not edit schema via the Supabase UI.

---

## Main Content

This repo treats EF Core migrations as the executable source of truth for the Postgres schema.
The MGF.DataMigrator project is the only migration runner; the WPF host does not apply migrations.

## Principles

- Do not make schema changes in the Supabase UI.
- Apply schema changes via EF migrations, executed by MGF.DataMigrator.
- Do not commit database secrets to git.
- Prefer the Supabase direct DB host (db.<project-ref>.supabase.co:5432) for migrations (dotnet ef + MGF.DataMigrator).
- Use the Supabase session pooler host (*.pooler.supabase.com:5432) for app runtime connections if you need pooling.

## Configuration sources (precedence)

Both dotnet ef (design-time) and MGF.DataMigrator (runtime) load config in this order:

1. config/appsettings.json (committed defaults)
2. config/appsettings.{Environment}.json (local dev secrets when Environment=Development)
3. environment variables (CI/prod)

For the canonical key list (MGF_ENV, MGF_DB_MODE, and Database connection strings), see env-vars.md.

## Local dev setup (recommended: config/appsettings.Development.json)

1) Populate `config/appsettings.Development.json` per dev-secrets.md (ensure Database:Dev:* keys are set).

Example formats (do not commit real secrets; see config/appsettings.Development.sample.json):

```text
Host=db.YOUR_REF.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;Ssl Mode=Require;Pooling=false
Host=YOUR_PROJECT.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.YOUR_REF;Password=YOUR_PASSWORD;Ssl Mode=Require
```

Note: for proper certificate validation use Ssl Mode=VerifyFull plus Root Certificate=... (Supabase provides a root cert). Trust Server Certificate is deprecated/no-op in recent Npgsql versions.

2) Create and run migrations:

```powershell
dotnet ef migrations add <Name> --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator
$env:MGF_DB_MODE = "direct"
dotnet run --project src/Data/MGF.DataMigrator
```

MGF.DataMigrator will:
- apply migrations
- seed core lookup tables idempotently (safe to run multiple times)

## Dev reset workflow (DEV ONLY)

Destructive Dev reset helpers have been removed from the repo to reduce accidental data loss.
If a full Dev reset is ever required, handle it manually outside the repo and then re-apply migrations:

```powershell
$env:MGF_ENV = "Dev"
$env:MGF_DB_MODE = "direct"
dotnet run --project src/Data/MGF.DataMigrator
```

Integration tests may truncate data between test classes and require explicit opt-in flags:

```powershell
$env:MGF_ENV = "Dev"
$env:MGF_ALLOW_DESTRUCTIVE = "true"
$env:MGF_DESTRUCTIVE_ACK = "I_UNDERSTAND"
dotnet test MGF.sln
```

## Where migrations live

- Folder: src/Data/MGF.Data/Migrations/
- Files: *_<Name>.cs, *_<Name>.Designer.cs, and AppDbContextModelSnapshot.cs

## Preflight: project_storage_roots unique index

Before applying the project_storage_roots unique index migration, check for duplicates:

```sql
SELECT project_id, storage_provider_key, root_key, COUNT(*) AS dup_count
FROM public.project_storage_roots
GROUP BY project_id, storage_provider_key, root_key
HAVING COUNT(*) > 1;
```

## Common EF commands (with explanations)

### List migrations

Shows migrations known to EF in MGF.Data:

```powershell
dotnet ef migrations list --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator
```

### Add a migration

Creates new migration files under src/Data/MGF.Data/Migrations/:

```powershell
dotnet ef migrations add <Name> --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator
```

### Apply migrations (update the database)

Recommended (applies migrations + seeds lookups):

```powershell
dotnet run --project src/Data/MGF.DataMigrator
```

EF CLI alternative (updates DB only; does not run custom seeding):

```powershell
dotnet ef database update --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator
```

### Remove the latest migration

Removes the most recent migration files (use with care):

```powershell
dotnet ef migrations remove --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator
```

Notes:
- If the migration has already been applied to a database, roll back first (or create a new revert migration) to avoid drift.

### Roll back to a previous migration

```powershell
dotnet ef database update <PreviousMigrationName> --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator
```

## Alternative: session env var (ad hoc)

For one PowerShell session, set required connection string env vars per env-vars.md, then run:

```powershell
$env:MGF_DB_MODE = "direct"
$env:MGF_ENV = "Dev"
dotnet run --project src/Data/MGF.DataMigrator
```

Or use scripts/set-dev-connection.ps1 to set the session env var without committing secrets.

## CI and production

Set the required connection string env vars and MGF_ENV per env-vars.md, then run:

```powershell
dotnet run --project src/Data/MGF.DataMigrator
```

## Teammate-safe commands

These are read-only and local-safe:

```powershell
dotnet build MGF.sln
dotnet ef migrations list --project src/Data/MGF.Data --startup-project src/Data/MGF.DataMigrator
```

---

## System Context

This guide governs how schema changes are created, reviewed, and applied across environments.

---

## Core Concepts

- EF migrations are the contract for schema evolution.
- MGF.DataMigrator is the only supported migration runner.

---

## How This Evolves Over Time

- Update when EF tooling, migration workflow, or CI automation changes.
- Add new guardrails when destructive workflows change.

---

## Common Pitfalls and Anti-Patterns

- Editing schema directly in Supabase UI.
- Running migrations against prod with local credentials.

---

## When to Change This Document

- Migration workflow, tooling, or guardrails change.

---

## Related Documents
- env-vars.md
- repo-workflow.md
- destructive-ops-audit.md

## Change Log
- 2026-01-10 - Updated local dev secrets workflow to repo-root config file.
- 2026-01-07 - Reformatted to documentation standards.
