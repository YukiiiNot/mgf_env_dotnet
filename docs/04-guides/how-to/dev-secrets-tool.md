# Dev Secrets Tool

> How-to for exporting, importing, and validating local developer secrets.

---

## MetaData

**Purpose:** Provide the supported workflow for using the dev secrets CLI.
**Scope:** Covers export, import, validation, wrapper scripts, and troubleshooting. Excludes policy details.
**Doc Type:** How-To
**Status:** Active
**Last Updated:** 2026-01-07

---

## TL;DR

- Export or import secrets with the DevSecrets CLI.
- Validate keys against the required inventory before use.
- Follow the dev secrets policy for allowed keys.

---

## Main Content

This tool exports and imports .NET User Secrets based on `tools/dev-secrets/secrets.required.json`.
Policy and allowed keys: dev-secrets.md.

## Commands

### Export (from your machine)

```powershell
dotnet run --project src/DevTools/MGF.DevSecretsCli/MGF.DevSecretsCli.csproj -- export --out dev-secrets.export.json
```

Optional:

```powershell
dotnet run --project src/DevTools/MGF.DevSecretsCli/MGF.DevSecretsCli.csproj -- export --out dev-secrets.export.json --verbose
```

### Import (on a new dev machine)

```powershell
dotnet run --project src/DevTools/MGF.DevSecretsCli/MGF.DevSecretsCli.csproj -- import --in dev-secrets.export.json
```

Dry run:

```powershell
dotnet run --project src/DevTools/MGF.DevSecretsCli/MGF.DevSecretsCli.csproj -- import --in dev-secrets.export.json --dry-run
```

### Validate (required keys present)

```powershell
dotnet run --project src/DevTools/MGF.DevSecretsCli/MGF.DevSecretsCli.csproj -- validate
```

## Wrapper scripts

Windows (PowerShell):

```powershell
tools\dev-secrets\export.ps1 --out dev-secrets.export.json
tools\dev-secrets\import.ps1 --in dev-secrets.export.json
```

macOS/Linux (bash):

```bash
tools/dev-secrets/export.sh --out dev-secrets.export.json
tools/dev-secrets/import.sh --in dev-secrets.export.json
```

## Troubleshooting

- dotnet missing: install the .NET SDK and ensure dotnet is on PATH.
- Missing UserSecretsId: check the project's csproj or secrets.required.json.
- Missing required keys: run devsecrets validate and set the missing keys.
- Validation errors: ensure your export JSON only includes keys from secrets.required.json.
- Policy violations: see dev-secrets.md.

## Smoke test (fake values)

```powershell
# Set fake values for a quick round-trip test
dotnet user-secrets set "Database:Dev:DirectConnectionString" "Host=localhost;Database=postgres;Username=postgres;Password=fake" --id 8f8e4093-a213-4629-bbd1-2a67c4e9000e

# Export
dotnet run --project src/DevTools/MGF.DevSecretsCli/MGF.DevSecretsCli.csproj -- export --out dev-secrets.export.json

# Import (dry-run)
dotnet run --project src/DevTools/MGF.DevSecretsCli/MGF.DevSecretsCli.csproj -- import --in dev-secrets.export.json --dry-run
```

---

## System Context

This guide supports local developer configuration workflows without exposing production secrets.

---

## Core Concepts

- The CLI enforces an allowlist of required keys.
- Export and import are local-only workflows governed by policy.

---

## How This Evolves Over Time

- Update when the CLI arguments or required keys inventory changes.
- Add new wrapper scripts if supported platforms change.

---

## Common Pitfalls and Anti-Patterns

- Exporting secrets that are not in the required keys list.
- Running import without validating required keys.

---

## When to Change This Document

- CLI flags, wrapper scripts, or required key inventory changes.

---

## Related Documents
- dev-secrets.md
- env-vars.md
- config-reference.md

## Change Log
- 2026-01-07 - Reformatted to documentation standards.
