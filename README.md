# MGF (.NET)

## Docs index (start here)

- `ROADMAP.md` — workflow coverage map + 30/60/90 day plan
- `ONBOARDING.md` — how to run locally and work safely
- `DEV_GUIDE.md` — coding conventions + integration patterns
- `docs/RUNBOOK_DELIVERY.md` — delivery end-to-end proof (Dev/Test)
- `docs/WORKFLOW.md` — repo workflow + CI/CD rules
- `docs/DB_WORKFLOW.md` — DB/migrations runbook
- `docs/infra/contracts.md` — infrastructure contract guarantees

## Database migrations (EF Core + Supabase)

MGF uses **EF Core migrations** as the executable source of truth for the Postgres schema.

- Workflow + commands: `docs/DB_WORKFLOW.md`
- Schema design CSV docs (design-time only): `docs/db_design/schema_csv/README.md`

## Internal API + worker

- `src/MGF.Api`: internal Web API (intended DB entrypoint for apps)
- `src/MGF.Worker`: background worker that processes rows in `public.jobs`

### Local run

```powershell
# required secrets (stored in user-secrets for MGF.Infrastructure)
# use DirectConnectionString when MGF_DB_MODE=direct
dotnet user-secrets set "Database:Dev:DirectConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
dotnet user-secrets set "Database:Dev:ConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
dotnet user-secrets set "Security:ApiKey" "<long random string>" --project src/MGF.Infrastructure

# apply migrations + seed lookups (recommended)
$env:MGF_ENV = "Dev"
$env:MGF_DB_MODE = "direct"
$env:MGF_CONFIG_DIR = "C:\\dev\\mgf_env_dotnet\\config"
dotnet run --project src/MGF.Tools.Migrator

# seed lookups only (skips migrations; use when schema is already up to date)
$env:MGF_ENV = "Dev"
$env:MGF_DB_MODE = "direct"
$env:MGF_CONFIG_DIR = "C:\\dev\\mgf_env_dotnet\\config"
dotnet run --project src/MGF.Tools.Migrator -- --seed-lookups

# run API (expects X-MGF-API-KEY header on /api/*)
dotnet run --project src/MGF.Api

# run worker (polls + executes jobs)
dotnet run --project src/MGF.Worker
```

## Square import tool

- `src/MGF.Tools.SquareImport`: imports Square CSV exports into the database

### Examples

```powershell
$env:MGF_ENV = "Dev"

# customers (Square CSV mode)
dotnet run --project src/MGF.Tools.SquareImport -- customers --mode square --file .\docs\square_imports\customers-20251215-182743.csv --dry-run

# customers (applied mode: re-run from customers-applied.csv)
dotnet run --project src/MGF.Tools.SquareImport -- customers --mode applied --file .\runtime\square-import\customers-applied.csv --dry-run

# customers: DB sanity checks (no CSV)
dotnet run --project src/MGF.Tools.SquareImport -- customers --verify

# customers: DEV-only reset (deletes Square-imported customer data only)
# requires explicit confirmation flag + interactive prompt
dotnet run --project src/MGF.Tools.SquareImport -- customers --reset --i-understand-this-will-destroy-data

# transactions (multiple files) + unmatched report
dotnet run --project src/MGF.Tools.SquareImport -- transactions --files `
  .\docs\square_imports\transactions-2023-01-01-2024-01-01.csv `
  .\docs\square_imports\transactions-2024-01-01-2025-01-01.csv `
  .\docs\square_imports\transactions-2025-01-01-2026-01-01.csv `
  --unmatched-report .\runtime\unmatched-transactions.csv `
  --dry-run

# invoices
dotnet run --project src/MGF.Tools.SquareImport -- invoices --file .\docs\square_imports\invoices-export-20251215T1853.csv --dry-run

# report
dotnet run --project src/MGF.Tools.SquareImport -- report --out .\runtime\square-import-report.txt
```

### Command reference

```powershell
# list all commands/options
dotnet run --project src/MGF.Tools.SquareImport -- --help

# customers
dotnet run --project src/MGF.Tools.SquareImport -- customers --help

# transactions
dotnet run --project src/MGF.Tools.SquareImport -- transactions --help

# invoices
dotnet run --project src/MGF.Tools.SquareImport -- invoices --help

# report
dotnet run --project src/MGF.Tools.SquareImport -- report --help
```

### Quick commands

```powershell
# repo health
dotnet build .\MGF.sln

# tests (includes DB integration tests; requires a DB connection string + destructive opt-in flags)
$env:MGF_ENV = "Dev"
$env:MGF_ALLOW_DESTRUCTIVE = "true"
$env:MGF_DESTRUCTIVE_ACK = "I_UNDERSTAND"
dotnet test .\MGF.sln

# list migrations
dotnet ef migrations list --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator

# add a migration
dotnet ef migrations add <Name> --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator

# remove the latest migration (only if not applied, or after rolling back)
dotnet ef migrations remove --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator

# apply migrations + seed lookups (recommended)
dotnet run --project src/MGF.Tools.Migrator

# seed lookups only (skips migrations; use when schema is already up to date)
dotnet run --project src/MGF.Tools.Migrator -- --seed-lookups
```

## Tooling / useful commands

### .NET SDK + runtime versions

This repo pins the SDK via `global.json`.

```powershell
# show pinned SDK for this repo
Get-Content .\global.json

# show which SDK dotnet is using in this folder
dotnet --version

# detailed SDK/runtime info (includes global.json resolution)
dotnet --info

# list installed SDKs/runtimes on this machine
dotnet --list-sdks
dotnet --list-runtimes
```

### EF tooling

```powershell
# show dotnet-ef version
dotnet ef --version

# remove the latest migration (only if not applied, or after rolling back)
dotnet ef migrations remove --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator

# update DB directly via EF CLI (DB only; does not run custom seeding)
dotnet ef database update --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
```

#### Rollback workflow (dev/staging only)

Rollback means “update the database to an earlier migration” (it does not delete migration files).

```powershell
# 1) list migrations so you know the exact previous migration name
dotnet ef migrations list --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator

# 2) roll the DB back to a specific previous migration
dotnet ef database update <PreviousMigrationName> --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator

# 3) (optional) roll all the way back to an empty DB (no migrations applied)
dotnet ef database update 0 --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator

# 4) if you also want to remove the latest migration from code, do it after rollback
dotnet ef migrations remove --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
```

Notes:
- `MGF.Tools.Migrator` only applies migrations forward (`MigrateAsync()`); use the EF CLI for rollbacks.
- Avoid rollback on production databases; prefer a new “fix-forward” migration instead through standard CI workflows.

### Configuration helpers (no secrets in git)

```powershell
# recommended local dev: store the connection string in user-secrets (MGF.Infrastructure)
dotnet user-secrets set "Database:Dev:ConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure

# select which DB to use (Dev is default)
$env:MGF_ENV = "Dev"

# alternative (ad-hoc): set a session env var (PowerShell)
$env:Database__Dev__ConnectionString = "<Npgsql connection string>"

# helper script to set Database__ConnectionString for the current session (prompts/params; no secrets committed)
.\scripts\set-dev-connection.ps1
```

Notes:
- `dotnet user-secrets list --project src/MGF.Infrastructure` will print secrets to the console; avoid screen sharing when you run it.

### Repo basics

```powershell
git status -sb
dotnet restore .\MGF.sln
dotnet clean .\MGF.sln
```
