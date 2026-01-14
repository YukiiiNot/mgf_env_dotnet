# Database Environment Variables (Dev / Staging / Prod)

> Contract for environment selection and database connection key names.

---

## MetaData

**Purpose:** Define environment selection and connection string key contracts for database access.
**Scope:** Covers MGF_ENV, MGF_DB_MODE, and Database:* connection string keys. Excludes host wiring and appsettings defaults.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-10

---

## TL;DR

- Use `MGF_ENV` to select Dev, Staging, or Prod.
- Use the `Database:<Env>:*ConnectionString` keys; legacy keys are fallback only.
- Destructive operations are guarded and blocked for non-Dev environments.
- Local dev secrets live in config/appsettings.Development.json; env vars override for CI/prod.

---

## Main Content

Source of truth: `src/Data/MGF.Data/Configuration/MgfConfiguration.cs`, `src/Data/MGF.Data/Options/Options.cs`, `config/appsettings*.json`

## Connection string keys

Use one of these per environment:

- `Database:Dev:ConnectionString`
- `Database:Dev:DirectConnectionString` (used when `MGF_DB_MODE=direct`)
- `Database:Dev:PoolerConnectionString` (used when `MGF_DB_MODE=pooler`)
- `Database:Staging:ConnectionString`
- `Database:Staging:DirectConnectionString` (used when `MGF_DB_MODE=direct`)
- `Database:Staging:PoolerConnectionString` (used when `MGF_DB_MODE=pooler`)
- `Database:Prod:ConnectionString`
- `Database:Prod:DirectConnectionString` (used when `MGF_DB_MODE=direct`)
- `Database:Prod:PoolerConnectionString` (used when `MGF_DB_MODE=pooler`)

Legacy fallback (only used when env-specific keys are missing):

- `Database:ConnectionString`

## Local dev secrets file

Local dev secrets live in `config/appsettings.Development.json` (git-ignored).
See dev-secrets.md for the canonical workflow.

Example (Npgsql format):

```text
Host=db.YOUR_REF.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;Ssl Mode=Require;Pooling=false
Host=YOUR_PROJECT.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.YOUR_REF;Password=YOUR_PASSWORD;Ssl Mode=Require
```

## Environment variables

Environment variables override config files.

```powershell
$env:MGF_DB_MODE = "direct"
$env:Database__Dev__ConnectionString = "<Npgsql connection string>"
$env:Database__Dev__DirectConnectionString = "<Npgsql connection string>"
$env:Database__Dev__PoolerConnectionString = "<Npgsql connection string>"
$env:Database__Staging__ConnectionString = "<Npgsql connection string>"
$env:Database__Staging__DirectConnectionString = "<Npgsql connection string>"
$env:Database__Staging__PoolerConnectionString = "<Npgsql connection string>"
$env:Database__Prod__ConnectionString = "<Npgsql connection string>"
$env:Database__Prod__DirectConnectionString = "<Npgsql connection string>"
$env:Database__Prod__PoolerConnectionString = "<Npgsql connection string>"
```

Legacy env var (fallback only):

```powershell
$env:Database__ConnectionString = "<Npgsql connection string>"
```

## Selecting the DB environment

Allowed values:
- `Dev` (default)
- `Staging`
- `Prod`

Per-session (PowerShell):

```powershell
$env:MGF_ENV = "Dev"
```

Persist for your user profile:

```powershell
setx MGF_ENV Dev
```

## Running migrations (Migrator)

```powershell
$env:MGF_DB_MODE = "direct"
dotnet run --project src/Data/MGF.DataMigrator
```

## Integration tests (destructive guardrails)

Integration tests truncate core tables in the target DB. Guardrails:

- If `MGF_ENV == "Prod"`: destructive ops are blocked.
- If `MGF_ENV != "Prod"`: destructive ops require `MGF_ALLOW_DESTRUCTIVE="true"` and `MGF_DESTRUCTIVE_ACK="I_UNDERSTAND"`.

Run tests safely:

```powershell
$env:MGF_ENV = "Dev"
$env:MGF_ALLOW_DESTRUCTIVE = "true"
$env:MGF_DESTRUCTIVE_ACK = "I_UNDERSTAND"
dotnet test MGF.sln
```

---

## System Context

Configuration contracts define the shared keys used by hosts, tools, and tests to select environments and connection strings.

---

## Core Concepts

- `MGF_ENV` selects which environment block is used for database configuration.
- `MGF_DB_MODE` selects between direct and pooler connection string keys.
- Legacy keys are fallback only and should not be relied on for new workflows.

---

## How This Evolves Over Time

- Update when a new environment or connection string key is introduced.
- Record changes to guardrails and destructive operation gates.

---

## Common Pitfalls and Anti-Patterns

- Using legacy `Database:ConnectionString` instead of env-specific keys.
- Running destructive workflows without setting Dev-only guardrails.
- Mixing direct and pooler modes without setting `MGF_DB_MODE`.

---

## When to Change This Document

- Environment selection rules or key names change.
- Destructive guardrail behavior changes.

---

## Related Documents
- config-reference.md
- dev-secrets.md
- db-migrations.md
- repo-workflow.md

## Change Log
- 2026-01-10 - Replaced user-secrets guidance with repo-root dev config workflow.
