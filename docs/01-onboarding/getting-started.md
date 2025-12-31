# Onboarding (MGF)

This guide gets a second developer productive without touching production.

## Repo layout (high level)

- `src/Services/MGF.Api` — internal Web API (DB entrypoint for apps)
- `src/Services/MGF.Worker` — background jobs runner
- `src/Operations/MGF.ProjectBootstrapCli` — CLI for bootstrap/archive/delivery/email/preview
- `src/Data/MGF.Tools.Migrator` — migrations + lookup seeding
- `src/Services/MGF.Worker/Email` — email subsystem (templates, senders, preview)
- `docs/` — runbooks, workflow, templates, contracts
- `tests/` — unit + contract tests

## Local config + precedence

Configuration is built by `AddMgfConfiguration` (see `src/Data/MGF.Infrastructure/Configuration/MgfConfiguration.cs`):

1. `MGF_CONFIG_DIR` (if set) -> `appsettings.json` and `appsettings.{ENV}.json`
2. Local `appsettings.json` / `appsettings.{ENV}.json`
3. BaseDir `appsettings.json` / `appsettings.{ENV}.json`
4. User-secrets (if project has UserSecretsId)
5. Environment variables

Common env vars:

```powershell
$env:MGF_ENV = 'Dev'
$env:MGF_DB_MODE = 'direct'
$env:MGF_CONFIG_DIR = 'C:\dev\mgf_env_dotnet\config'
```

## Required secrets (Dev)

Store secrets in `MGF.Infrastructure` user-secrets (no secrets in git):

```powershell
dotnet user-secrets set "Database:Dev:DirectConnectionString" "<Npgsql connection string>" --project src/Data/MGF.Infrastructure
dotnet user-secrets set "Security:ApiKey" "<api key>" --project src/Data/MGF.Infrastructure
```

## Core workflows (Dev)

### Bootstrap / Archive / Delivery

Use the ProjectBootstrap CLI and the delivery runbook:

- `docs/05-runbooks/delivery.md`

Common commands:

```powershell
# mark ready_to_provision
dotnet run --project src\Operations\MGF.ProjectBootstrapCli -- ready --projectId <PROJECT_ID>

# bootstrap
dotnet run --project src\Operations\MGF.ProjectBootstrapCli -- enqueue --projectId <PROJECT_ID> --editors TE --verifyDomainRoots true --createDomainRoots true --provisionProjectContainers true

# deliver (see docs/05-runbooks/delivery.md for the full sequence)
dotnet run --project src\Operations\MGF.ProjectBootstrapCli -- to-deliver --projectId <PROJECT_ID>
dotnet run --project src\Operations\MGF.ProjectBootstrapCli -- deliver --projectId <PROJECT_ID> --editorInitials TE
```

### Email preview (no send)

```powershell
dotnet run --project src\Operations\MGF.ProjectBootstrapCli -- email-preview --fixture basic --out .\runtime\email_preview
```

## Tests to run

```powershell
# Worker tests
dotnet test -c Release tests\MGF.Worker.Tests\MGF.Worker.Tests.csproj

# ProjectBootstrap CLI tests
dotnet test -c Release tests\MGF.ProjectBootstrapCli.Tests\MGF.ProjectBootstrapCli.Tests.csproj
```

## Where to start (new dev)

1. Read `docs/02-architecture/roadmap.md` and pick a small, isolated milestone.
2. Read `docs/05-runbooks/delivery.md` to understand the current "golden path."
3. Run the relevant tests for your area.
4. Make one focused change, update tests/runbook, and keep PRs small.

## Glossary

- **Stable Final**: the delivery folder `...\01_Deliverables\Final` shared with clients.
- **Version folder**: `Final\vN` under the stable Final folder.
- **Delivery container**: Dropbox container under `04_Client_Deliveries/<Client>/<ProjectCode>_<ProjectName>`.
- **EmailKind**: enum identifying an email type (delivery_ready, etc.).
- **EmailProfile**: sender policy (allowed From addresses, defaults).
- **Root contract**: required/optional top-level folders for Dropbox/LucidLink/NAS roots.

