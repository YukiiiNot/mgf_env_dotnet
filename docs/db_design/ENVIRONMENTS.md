# Database environments (Dev / Staging / Prod)

This repo supports selecting which Postgres/Supabase database to use via `MGF_ENV`.

## Connection string keys

Use **one** of these per environment:

- `Database:Dev:ConnectionString`
- `Database:Staging:ConnectionString`
- `Database:Prod:ConnectionString`

Legacy fallback (only used when the env-specific key is missing):

- `Database:ConnectionString`

## Recommended: store secrets in user-secrets (local)

User-secrets are per-developer and are **not committed to git**.

```powershell
dotnet user-secrets init --project src/MGF.Infrastructure

dotnet user-secrets set "Database:Dev:ConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
dotnet user-secrets set "Database:Staging:ConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
dotnet user-secrets set "Database:Prod:ConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure
```

Example (Npgsql format):

```text
Host=YOUR_HOST;Port=5432;Database=postgres;Username=postgres.YOUR_REF;Password=YOUR_PASSWORD;Ssl Mode=Require;Trust Server Certificate=true
```

## Alternative: environment variables

Environment variables override all config files and user-secrets.

```powershell
$env:Database__Dev__ConnectionString = "<Npgsql connection string>"
$env:Database__Staging__ConnectionString = "<Npgsql connection string>"
$env:Database__Prod__ConnectionString = "<Npgsql connection string>"
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

