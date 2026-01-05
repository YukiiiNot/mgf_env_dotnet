# Migrations Ci

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

# Migrations CI Runbook

Source of truth: `.github/workflows/migrate-staging.yml`, `.github/workflows/migrate-prod.yml`, `.github/workflows/ci.yml`
Change control: Update when migration pipelines or CI gating changes.
Last verified: 2025-12-30

## Summary
- Staging migrations: `.github/workflows/migrate-staging.yml`
- Production migrations: `.github/workflows/migrate-prod.yml`
- CI checks: `.github/workflows/ci.yml`

## Related docs
- DB migrations how-to: [../04-guides/how-to/db-migrations.md](../04-guides/how-to/db-migrations.md)

---

## Metadata

Last updated: 2026-01-02  
Owner: Operations  
Review cadence: after incident or change  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
