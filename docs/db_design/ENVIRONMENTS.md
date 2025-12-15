# Database environments (Dev / Staging / Prod)

This repo supports selecting which Postgres/Supabase database to use via `MGF_ENV`.

## Connection string keys

Use **one** of these per environment:

- `Database:Dev:ConnectionString`
- `Database:Dev:DirectConnectionString` (used when `MGF_DB_MODE=direct`)
- `Database:Dev:PoolerConnectionString` (used when `MGF_DB_MODE=pooler`)
- `Database:Staging:ConnectionString`
- `Database:Staging:DirectConnectionString` (used when `MGF_DB_MODE=direct`)
- `Database:Staging:PoolerConnectionString` (used when `MGF_DB_MODE=pooler`)
- `Database:Prod:ConnectionString`
- `Database:Prod:DirectConnectionString` (used when `MGF_DB_MODE=direct`)
- `Database:Prod:PoolerConnectionString` (used when `MGF_DB_MODE=pooler`)

Legacy fallback (only used when the env-specific key is missing):

- `Database:ConnectionString`

## Recommended: store secrets in user-secrets (local)

User-secrets are per-developer and are **not committed to git**.

```powershell
dotnet user-secrets init --project src/MGF.Infrastructure

dotnet user-secrets set "Database:Dev:DirectConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
dotnet user-secrets set "Database:Dev:PoolerConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
dotnet user-secrets set "Database:Staging:DirectConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
dotnet user-secrets set "Database:Staging:PoolerConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
dotnet user-secrets set "Database:Prod:DirectConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
dotnet user-secrets set "Database:Prod:PoolerConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
```

Example (Npgsql format):

```text
Host=db.YOUR_REF.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;Ssl Mode=Require;Pooling=false
Host=YOUR_PROJECT.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.YOUR_REF;Password=YOUR_PASSWORD;Ssl Mode=Require
```

## Alternative: environment variables

Environment variables override all config files and user-secrets.

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
dotnet run --project src/MGF.Tools.Migrator
```

## Integration tests (destructive guardrails)

Integration tests **TRUNCATE** core tables in the target DB. Guardrails:

- If `MGF_ENV == "Prod"`: destructive ops are **blocked always**.
- If `MGF_ENV != "Prod"`: destructive ops require `MGF_ALLOW_DESTRUCTIVE == "true"`.

Run tests safely:

```powershell
$env:MGF_ENV = "Dev"
$env:MGF_ALLOW_DESTRUCTIVE = "true"
dotnet test .\MGF.sln
```

Never set `MGF_ALLOW_DESTRUCTIVE=true` for Prod (Prod blocks it anyway).
