# Getting Started

> Quick path to safe local setup and the right source-of-truth docs.

---

## MetaData

**Purpose:** Get new contributors productive locally without risking production workflows, including UI-first contributors.
**Scope:** Local setup, repo orientation, and safe first verification steps. Does not replace runbooks or deep architecture guides.
**Doc Type:** Guide
**Status:** Active
**Last Updated:** 2026-01-06

---

## TL;DR

- Learn the bucket model before adding code.
- Use DevSecrets for local secrets and follow the standard verification steps.
- Use runbooks for operations and architecture docs for design decisions.

---

## Main Content

1. Set environment markers for local development:

```powershell
$env:MGF_ENV = 'Dev'
$env:MGF_DB_MODE = 'direct'
$env:MGF_CONFIG_DIR = 'C:\dev\mgf_env_dotnet\config'
```

2. Bootstrap secrets using DevSecrets (preferred)

DevSecrets exists so new devs do not manually type many secrets and risk drift.

Read and follow:
- dev-secrets-tool.md

*Configuration precedence (overview)*

Configuration is built by `AddMgfConfiguration`. The precedence rules are documented here:
- configuration
- Optional code path reference: src/Data/MGF.Data/Configuration/MgfConfiguration.cs

3. First verification steps (before you change code)

Run baseline checks:
- dotnet build MGF.sln -c Release
- dotnet test MGF.sln -c Release --filter FullyQualifiedName!~MGF.Data.IntegrationTests

Then verify key workflows using the sanctioned verification process:
- testing-and-verification.md
- 05-runbooks (pick the relevant workflow runbook)

If you want a UI-first workflow view (for visual onboarding), start with a dev console UI that:
- calls API/Operations surfaces safely
- shows job queue state and last workflow status
- never bypasses UseCases

---

## System Context

This guide sits at the entry point of the repo documentation. It points readers to the canonical architecture, contracts, and runbook sources of truth and should not introduce new rules.

---

## Core Concepts

- The system is organized by buckets; placement and dependency direction matter.
- Local setup should be repeatable and should not rely on hand-managed secrets.
- Verification steps are part of safe onboarding, not optional extras.

---

## How This Evolves Over Time

- Update when onboarding steps change (new tools or new setup requirements).
- Update when canonical docs move or new onboarding-critical workflows appear.

---

## Common Pitfalls and Anti-Patterns

- Skipping verification steps before changing code.
- Treating this guide as the authoritative source instead of linking to canonical docs.
- Duplicating architecture or runbook guidance here.

---

## When to Change This Document

- A required onboarding step changes or is replaced.
- Canonical docs change location or scope.
- A new safe-first workflow is required before contributing.

---

## Related Documents
- 00-index.md
- dev-guide.md
- contributing.md
- system-overview.md
- project-shapes.md
- testing-and-verification.md
- dev-secrets-tool.md

## Change Log
- Date format: YYYY-MM-DD (see doc-enumerations.md)
- 2026-01-06 - Reformatted into the new documentation standards format; content preserved.
- 2026-01-02 - Reformatted to the documentation template.
