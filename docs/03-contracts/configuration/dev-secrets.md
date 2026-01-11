# Dev Secrets Inventory

> Contract for allowed developer secrets and export/import policy.

---

## MetaData

**Purpose:** Define the allowed dev secrets keys and the export/import policy.
**Scope:** Covers required/optional keys and local dev workflow. Excludes prod/staging secrets.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-10

---

## TL;DR

- Dev secrets live only in repo-root `config/appsettings.Development.json` (git-ignored).
- DevSecretsCli import populates that file from a bundle; export creates a bundle.
- Allowed keys are defined in `tools/dev-secrets/secrets.required.json`.

---

## Policy (local dev secrets)

- Dev secrets live in repo-root `config/appsettings.Development.json` (git-ignored).
- Environment variables are overrides for CI/prod or debugging, not the baseline.
- .NET user-secrets are not used for runtime.
- DevSecretsCli is onboarding-only (import once, then use the local config file).

---

## Main Content

Source of truth: `tools/dev-secrets/secrets.required.json`, `src/DevTools/MGF.DevSecretsCli`

This inventory lists the developer secrets that can be exported/imported for local dev only.

## Local dev secrets file

- Single source of truth: `config/appsettings.Development.json` (git-ignored).
- Initialize by copying `config/appsettings.Development.sample.json` or by running DevSecretsCli import.
- .NET user-secrets are not used at runtime.

## Required keys (local dev)

- `Database:Dev:DirectConnectionString`
- `Security:ApiKey`
- `Api:BaseUrl`

## Optional keys (local dev)

- `Security:Operator`
- `Integrations:Dropbox:*`
- `Integrations:Email:*`

## Export and import behavior

- Export reads `config/appsettings.Development.json` and writes `dev-secrets.export.json`.
- Import merges a bundle into `config/appsettings.Development.json`.
- Use `--force` to overwrite existing values.
- Validate reports missing keys by name only (never values).
- Export/import is local-only; prod/staging/CI secrets are never allowed.

## Smoke checklist (local dev)

1) Run API (Development):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/Services/MGF.Api
```

2) Verify API responds (use the key from config/appsettings.Development.json):

```powershell
Invoke-WebRequest http://localhost:5048/api/meta -Headers @{ "X-MGF-API-KEY" = "<your key>" }
```

3) Run DevConsole (Development):

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project src/Ui/MGF.DevConsole.Desktop
```

Expected: StartupGate passes and DevConsole shows Connected.

## Where keys are used

- **Database:** `Database:Dev:DirectConnectionString` is used by local services and tools.
- **API key:** `Security:ApiKey` is required by API and DevConsole for `/api/*`.
- **Api base URL:** `Api:BaseUrl` is used by DevConsole to reach the API.
- **Integrations:** Dropbox/Email keys are used by delivery workflows.

No production or staging secrets should appear in local developer config.

---

## System Context

Dev secrets define the local-only configuration boundary used by hosts and tools without exposing production credentials.

---

## Core Concepts

- Dev secrets are constrained to a small, explicit allowlist.
- Export/import is driven by the required keys inventory and policy checks.

---

## How This Evolves Over Time

- Update when required or optional dev secrets change.
- Revisit disallowed patterns when new environments or providers are added.

---

## Common Pitfalls and Anti-Patterns

- Adding secrets outside the allowlist.
- Introducing prod or staging keys into local exports.

---

## When to Change This Document

- The required keys inventory changes.
- Secret policy or tool behavior changes.

---

## Related Documents
- dev-secrets-tool.md
- env-vars.md
- config-reference.md
- integrations.md

## Change Log
- 2026-01-10 - Added smoke checklist and clarified validate output.
- 2026-01-10 - Added explicit policy block for local dev secrets handling.
- 2026-01-10 - Updated to single-source local dev secrets file and CLI import/export workflow.
