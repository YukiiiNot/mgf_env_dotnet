# Destructive Ops Audit

> Runbook for auditing destructive operations and guardrails in the repo.

---

## MetaData

**Purpose:** Document destructive or reset-like operations and their guardrails.
**Scope:** Covers known destructive operations and safety checks. Excludes implementation details.
**Doc Type:** Runbook
**Status:** Active
**Last Updated:** 2026-01-07

---

## TL;DR

- Known destructive operations are explicitly gated and Dev-only.
- Integration tests truncate core tables but require guardrails.
- Square import reset requires explicit destructive flags and confirmation.

---

## Main Content

Source of truth: tests/MGF.Data.IntegrationTests, MGF.SquareImportCli

This report lists destructive or reset-like operations discovered in the repo after removing the reset-dev scripts.
Each entry includes the file path, a short snippet, current guardrails, and a recommendation.

## Removed footguns (by design)

- tools/reset-dev.ps1 (deleted)
- tools/reset-dev.sql (deleted)
- DB_RESET_DEV.md (deleted)

These scripts dropped and recreated the public schema and were removed to reduce accidental data loss.

## Remaining destructive operations

### 1) Integration tests TRUNCATE core tables (Dev-only, gated)

Path: tests/MGF.Data.IntegrationTests/DatabaseFixture.cs

Snippet:

```csharp
DatabaseConnection.EnsureDestructiveAllowedOrThrow("Integration tests (will TRUNCATE core tables)");
...
await db.Database.ExecuteSqlRawAsync(
    """
    TRUNCATE TABLE
      public.booking_attendees,
      public.bookings,
      public.project_members,
      public.project_storage_roots,
      public.projects,
      public.people,
      public.clients
    CASCADE;
    """
);
```

Guardrails:
- Requires MGF_ENV=Dev
- Requires MGF_ALLOW_DESTRUCTIVE=true and MGF_DESTRUCTIVE_ACK=I_UNDERSTAND
- Blocks if connection string looks non-dev (prod, production, staging, stage, uat, preprod, live)

Recommendation: Keep as-is. This is a known destructive test fixture and is already gated.

Intentional run (Dev only):

```powershell
$env:MGF_ENV = "Dev"
$env:MGF_ALLOW_DESTRUCTIVE = "true"
$env:MGF_DESTRUCTIVE_ACK = "I_UNDERSTAND"
dotnet test MGF.sln
```

---

### 2) Square import customers reset (Dev-only, gated)

Path: src/DevTools/MGF.SquareImportCli/Importers/CustomersImporter.cs

Snippet:

```csharp
if (env != MgfEnvironment.Dev)
{
    Console.Error.WriteLine($"square-import customers: --reset blocked (DEV only). Current MGF_ENV={env}.");
    return new ImportSummary(Inserted: 0, Updated: 0, Skipped: 0, Errors: 1);
}
...
clientsRemoved += await db.Clients
    .Where(c => c.ClientId == clientId)
    .ExecuteDeleteAsync(cancellationToken);
```

Path: src/DevTools/MGF.SquareImportCli/Commands/CustomersCommand.cs

Snippet:

```csharp
if (!destructive)
{
    Console.Error.WriteLine("square-import customers: --reset requires --i-understand-this-will-destroy-data to proceed.");
    ...
}
...
Console.Write("Type RESET to confirm destructive customer reset: ");
```

Guardrails:
- Explicit CLI opt-in: --reset --i-understand-this-will-destroy-data
- Interactive confirmation prompt (unless --non-interactive)
- Dev-only (MGF_ENV=Dev)
- Refuses if connection string looks non-dev (contains prod, production, or staging)

Recommendation: Keep, but ensure documentation references the explicit destructive flag and prompt.

Intentional run (Dev only):

```powershell
$env:MGF_ENV = "Dev"
dotnet run --project src/DevTools/MGF.SquareImportCli -- customers --reset --i-understand-this-will-destroy-data
```

---

## Non-destructive maintenance commands (not data-wiping)

These commands update job states but do not delete application data:

Path: src/Operations/MGF.ProjectBootstrapCli/Program.cs

Snippet (jobs reaper):

```csharp
UPDATE public.jobs
SET status_key = 'queued',
    run_after = now(),
    locked_by = NULL,
    locked_until = NULL,
    last_error = CASE
      WHEN locked_until IS NULL
        THEN 'reaped stale running job (no lock, started_at stale)'
      ELSE 'reaped stale running job (expired lock)'
    END
WHERE status_key = 'running'
  AND ...
```

Recommendation: Keep as-is. This is a safety mechanism for stuck jobs and is not destructive to business data.

## Summary

- All known reset scripts were removed.
- Remaining destructive operations are limited to:
  - Dev integration tests (TRUNCATE) with explicit guardrails.
  - Square-import customer reset with explicit flag, confirmation, and Dev-only checks.
- No destructive actions run by default on build, test, or runtime.

---

## System Context

This runbook documents destructive operations so that operators and engineers can assess risk and guardrails.

---

## Core Concepts

- Destructive operations are Dev-only and require explicit opt-in.
- Guardrails block destructive actions against non-dev environments.

---

## How This Evolves Over Time

- Update when new destructive commands are introduced or removed.
- Add guardrails when new destructive workflows appear.

---

## Common Pitfalls and Anti-Patterns

- Running destructive commands without explicit opt-in flags.
- Using production-like credentials in Dev tooling.

---

## When to Change This Document

- Destructive operations, guardrails, or tooling behavior change.

---

## Related Documents
- repo-workflow.md
- db-migrations.md
- env-vars.md

## Change Log
- 2026-01-07 - Reformatted to documentation standards.
