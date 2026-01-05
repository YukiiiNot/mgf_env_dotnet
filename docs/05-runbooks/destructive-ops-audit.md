# Destructive Ops Audit

Purpose  
Provide an operational runbook for this workflow.

Audience  
Operators and engineers running production or dev workflows.

Scope  
Covers the operational procedure and required checks. Does not define system design.

Status  
Active

---

## Key Takeaways

- This runbook provides the approved operational procedure.
- Follow prerequisites and post-checks to confirm success.
- Escalate if conditions or data are outside documented bounds.

---

## System Context

This runbook supports operational execution across Services, UseCases, and Data.

---

## Core Concepts

Follow the documented procedure. Context and prerequisites are captured here; the detailed steps are in the appendix.

---

## How This Evolves Over Time

- Update steps when tooling or operational flow changes.
- Add new checks when new failure modes appear.

---

## Common Pitfalls and Anti-Patterns

- Skipping prerequisites or post-checks.
- Running commands in the wrong environment.

---

## When to Change This Document

- Operational steps or prerequisites change.
- New failure modes or checks are introduced.

---

## Related Documents

- ../01-onboarding/dev-guide.md
- ../02-architecture/workflows.md
- repo-workflow.md

---

## Appendix (Optional)

### Prior content (preserved for reference)

# Destructive Operations Audit

Source of truth: `tests/MGF.Data.IntegrationTests/DatabaseFixture.cs`, `src/DevTools/MGF.SquareImportCli/Importers/CustomersImporter.cs`, `src/DevTools/MGF.SquareImportCli/Commands/CustomersCommand.cs`
Change control: Update when destructive operations or guardrails change.
Last verified: 2025-12-30


This report lists destructive or reset-like operations discovered in the repo after removing the `reset-dev` scripts.
Each entry includes the exact file path, a short snippet, current guardrails, and a recommendation.

## Removed footguns (by design)

- `tools/reset-dev.ps1` (deleted)
- `tools/reset-dev.sql` (deleted)
- `docs/DB_RESET_DEV.md` (deleted)

These scripts dropped/recreated the `public` schema and were removed to reduce accidental data loss.

## Remaining destructive operations

### 1) Integration tests TRUNCATE core tables (DEV-only, gated)

**Path:** `tests/MGF.Data.IntegrationTests/DatabaseFixture.cs`  
**Snippet:**

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

**Guardrails:**  
- Requires `MGF_ENV=Dev`  
- Requires `MGF_ALLOW_DESTRUCTIVE=true` and `MGF_DESTRUCTIVE_ACK=I_UNDERSTAND`  
- Blocks if connection string looks non-dev (`prod`, `production`, `staging`, `stage`, `uat`, `preprod`, `live`)

**Recommendation:** Keep as-is. This is a known destructive test fixture and is already gated.

**Intentional run (Dev only):**

```powershell
$env:MGF_ENV = "Dev"
$env:MGF_ALLOW_DESTRUCTIVE = "true"
$env:MGF_DESTRUCTIVE_ACK = "I_UNDERSTAND"
dotnet test .\MGF.sln
```

---

### 2) Square import customers reset (DEV-only, gated)

**Path:** `src/DevTools/MGF.SquareImportCli/Importers/CustomersImporter.cs`  
**Snippet:**

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

**Path:** `src/DevTools/MGF.SquareImportCli/Commands/CustomersCommand.cs`  
**Snippet:**

```csharp
if (!destructive)
{
    Console.Error.WriteLine("square-import customers: --reset requires --i-understand-this-will-destroy-data to proceed.");
    ...
}
...
Console.Write("Type RESET to confirm destructive customer reset: ");
```

**Guardrails:**  
- Explicit CLI opt-in: `--reset --i-understand-this-will-destroy-data`  
- Interactive confirmation prompt (unless `--non-interactive`)  
- DEV-only (`MGF_ENV=Dev`)  
- Refuses if connection string looks non-dev (contains `prod`, `production`, or `staging`)

**Recommendation:** Keep, but ensure documentation references the explicit destructive flag and prompt.

**Intentional run (Dev only):**

```powershell
$env:MGF_ENV = "Dev"
dotnet run --project src/DevTools/MGF.SquareImportCli -- customers --reset --i-understand-this-will-destroy-data
```

---

## Non-destructive maintenance commands (not data-wiping)

These commands update job states but do not delete application data:

**Path:** `src/Operations/MGF.ProjectBootstrapCli/Program.cs`  
**Snippet (jobs reaper):**

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

**Recommendation:** Keep as-is. This is a safety mechanism for stuck jobs and is not destructive to business data.

## Summary

- All known reset scripts were removed.  
- Remaining destructive operations are limited to:
  - Dev integration tests (TRUNCATE) -- explicitly gated.
  - Square-import customer reset -- explicitly gated with a destructive flag + prompt + Dev-only checks.  
- No destructive actions run by default on build/test/runtime.

---

## Metadata

Last updated: 2026-01-02  
Owner: Operations  
Review cadence: after incident or change  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
