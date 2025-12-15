# MGF (.NET)

## Database migrations (EF Core + Supabase)

MGF uses **EF Core migrations** as the executable source of truth for the Postgres schema.

- Workflow + commands: `docs/DB_WORKFLOW.md`
- Schema design CSV docs (design-time only): `docs/db_design/schema_csv/README.md`

### Quick commands

```powershell
# repo health
dotnet build .\MGF.sln
dotnet test .\MGF.sln

# list migrations
dotnet ef migrations list --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator

# add a migration
dotnet ef migrations add <Name> --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator

# remove the latest migration (only if not applied, or after rolling back)
dotnet ef migrations remove --project src/MGF.Infrastructure --startup-project src/MGF.Tools.Migrator

# apply migrations + seed lookups (recommended)
dotnet run --project src/MGF.Tools.Migrator
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

### Configuration helpers (no secrets in git)

```powershell
# recommended local dev: store the connection string in user-secrets (MGF.Infrastructure)
dotnet user-secrets set "Database:ConnectionString" "<Npgsql connection string>" --project src/MGF.Infrastructure

# alternative (ad-hoc): set a session env var (PowerShell)
$env:Database__ConnectionString = "<Npgsql connection string>"

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
