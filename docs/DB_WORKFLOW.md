# Database workflow (Supabase + EF Core)

This repo treats **EF Core migrations** as the executable source of truth for the Postgres schema.
The `MGF.Tools.Migrator` project is the **only** migration runner; the WPF host does not apply migrations.

## Principles

- Do **not** make schema changes in the Supabase UI.
- Apply schema changes via **EF migrations**, executed by `MGF.Tools.Migrator`.
- Do **not** commit database secrets to git.
- Use the Supabase **session pooler** host (`*.pooler.supabase.com:5432`) for migrations/runtime.

## Configuration sources (precedence)

Both `dotnet ef` (design-time) and `MGF.Tools.Migrator` (runtime) load config in this order:

1. `config/appsettings.json` (committed defaults)
2. `config/appsettings.{Environment}.json` (committed, non-secret overrides)
3. user-secrets (local dev)
4. environment variables (CI/prod)

The connection string key is: `Database:ConnectionString` (env var form: `Database__ConnectionString`).

## Local dev setup (recommended: user-secrets)

1) Set a local connection string (Npgsql format):

```powershell
dotnet user-secrets set "Database:ConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
```

Example format (do not commit real secrets; see `config/appsettings.Development.sample.json`):

```text
Host=YOUR_PROJECT.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.YOUR_REF;Password=YOUR_PASSWORD;Ssl Mode=Require;Trust Server Certificate=true
```

2) Run migrations:

```powershell
dotnet ef migrations add <Name> --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
dotnet run --project src/MGF.Tools.Migrator
```

`MGF.Tools.Migrator` will:
- apply migrations
- seed core lookup tables idempotently (safe to run multiple times)

## Alternative: session env var (ad-hoc)

For one PowerShell session:

```powershell
$env:Database__ConnectionString = "<Npgsql connection string>"
dotnet run --project src/MGF.Tools.Migrator
```

Or use `scripts/set-dev-connection.ps1` to set the session env var without committing secrets.

## CI / production

Set `Database__ConnectionString` as a secret environment variable in the deployment pipeline, then run:

```powershell
dotnet run --project src/MGF.Tools.Migrator
```

## Teammate-safe commands

These are read-only / local-safe:

```powershell
dotnet build .\MGF.sln
dotnet ef migrations list --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator
```

