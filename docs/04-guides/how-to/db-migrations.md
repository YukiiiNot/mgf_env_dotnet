# Database workflow (Supabase + EF Core)

This repo treats **EF Core migrations** as the executable source of truth for the Postgres schema.
The `MGF.Tools.Migrator` project is the **only** migration runner; the WPF host does not apply migrations.

## Principles

- Do **not** make schema changes in the Supabase UI.
- Apply schema changes via **EF migrations**, executed by `MGF.Tools.Migrator`.
- Do **not** commit database secrets to git.
- Prefer the Supabase **direct DB host** (`db.<project-ref>.supabase.co:5432`) for migrations (`dotnet ef` + `MGF.Tools.Migrator`).
- Use the Supabase **session pooler** host (`*.pooler.supabase.com:5432`) for app runtime connections if you need pooling at the edge.

## Configuration sources (precedence)

Both `dotnet ef` (design-time) and `MGF.Tools.Migrator` (runtime) load config in this order:

1. `config/appsettings.json` (committed defaults)
2. `config/appsettings.{Environment}.json` (committed, non-secret overrides)
3. user-secrets (local dev)
4. environment variables (CI/prod)

Connection string keys (selected by `MGF_ENV`, default `Dev`):

- `Database:Dev:ConnectionString`
- `Database:Dev:DirectConnectionString` (used when `MGF_DB_MODE=direct`)
- `Database:Dev:PoolerConnectionString` (used when `MGF_DB_MODE=pooler`)
- `Database:Staging:ConnectionString`
- `Database:Staging:DirectConnectionString` (used when `MGF_DB_MODE=direct`)
- `Database:Staging:PoolerConnectionString` (used when `MGF_DB_MODE=pooler`)
- `Database:Prod:ConnectionString`
- `Database:Prod:DirectConnectionString` (used when `MGF_DB_MODE=direct`)
- `Database:Prod:PoolerConnectionString` (used when `MGF_DB_MODE=pooler`)

Database mode selection (optional, defaults to `Auto`):

- `MGF_DB_MODE=direct` (prefer `*DirectConnectionString` keys)
- `MGF_DB_MODE=pooler` (prefer `*PoolerConnectionString` keys)

Legacy fallback (only used when the env-specific key is missing):

- `Database:ConnectionString` (env var form: `Database__ConnectionString`)

## Local dev setup (recommended: user-secrets)

1) Set a local connection string (Npgsql format):

```powershell
dotnet user-secrets set "Database:Dev:DirectConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
dotnet user-secrets set "Database:Dev:PoolerConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
```

Example formats (do not commit real secrets; see `config/appsettings.Development.sample.json`):

```text
Host=db.YOUR_REF.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;Ssl Mode=Require;Pooling=false
Host=YOUR_PROJECT.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.YOUR_REF;Password=YOUR_PASSWORD;Ssl Mode=Require
```

Note: for proper certificate validation use `Ssl Mode=VerifyFull` plus `Root Certificate=...` (Supabase provides a root cert). `Trust Server Certificate` is deprecated/no-op in recent Npgsql versions.

2) Create/run migrations:

```powershell
dotnet ef migrations add <Name> --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
$env:MGF_DB_MODE = "direct"
dotnet run --project src/MGF.Tools.Migrator
```

`MGF.Tools.Migrator` will:
- apply migrations
- seed core lookup tables idempotently (safe to run multiple times)

## Dev reset workflow (DEV ONLY)

Destructive Dev reset helpers have been removed from the repo to reduce accidental data loss.
If a full Dev reset is ever required, handle it manually outside the repo and then re-apply migrations:

```powershell
$env:MGF_ENV = "Dev"
$env:MGF_DB_MODE = "direct"
dotnet run --project src/MGF.Tools.Migrator
```

Integration tests may truncate data between test classes and require explicit opt-in flags:

```powershell
$env:MGF_ENV = "Dev"
$env:MGF_ALLOW_DESTRUCTIVE = "true"
$env:MGF_DESTRUCTIVE_ACK = "I_UNDERSTAND"
dotnet test .\MGF.sln
```

## Where migrations live

- Folder: `src/MGF.Infrastructure/Migrations/`
- Files: `*_<Name>.cs`, `*_<Name>.Designer.cs`, and `AppDbContextModelSnapshot.cs`

## Preflight: project_storage_roots unique index

Before applying the `project_storage_roots` unique index migration, check for duplicates:

```sql
SELECT project_id, storage_provider_key, root_key, COUNT(*) AS dup_count
FROM public.project_storage_roots
GROUP BY project_id, storage_provider_key, root_key
HAVING COUNT(*) > 1;
```

## Common EF commands (with explanations)

### List migrations

Shows migrations known to EF in `MGF.Infrastructure`:

```powershell
dotnet ef migrations list --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
```

### Add a migration

Creates new migration files under `src/MGF.Infrastructure/Migrations/`:

```powershell
dotnet ef migrations add <Name> --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
```

### Apply migrations (update the database)

Recommended (applies migrations + seeds lookups):

```powershell
dotnet run --project src/MGF.Tools.Migrator
```

EF CLI alternative (updates DB only; does not run custom seeding):

```powershell
dotnet ef database update --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
```

### Remove the latest migration

Removes the most recent migration files (use with care):

```powershell
dotnet ef migrations remove --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
```

Notes:
- If the migration has already been applied to a database, roll back first (or create a new â€œrevertâ€ migration) to avoid drift.

### Roll back to a previous migration

```powershell
dotnet ef database update <PreviousMigrationName> --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
```

## Alternative: session env var (ad-hoc)

For one PowerShell session:

```powershell
$env:MGF_DB_MODE = "direct"
$env:Database__Dev__DirectConnectionString = "<Npgsql connection string>"
$env:MGF_ENV = "Dev"
dotnet run --project src/MGF.Tools.Migrator
```

Or use `scripts/set-dev-connection.ps1` to set the session env var without committing secrets.

## CI / production

Set `Database__Prod__DirectConnectionString`, `MGF_DB_MODE=direct`, and `MGF_ENV=Prod` as secret environment variables in the deployment pipeline, then run:

```powershell
dotnet run --project src/MGF.Tools.Migrator
```

## Teammate-safe commands

These are read-only / local-safe:

```powershell
dotnet build .\MGF.sln
dotnet ef migrations list --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
```
