# Database Migrations Contract

Purpose  
Define the contract boundary and expectations for this area.

Audience  
Engineers building or consuming contracts and integrations.

Scope  
Covers contract intent and boundary expectations. Does not describe host wiring.

Status  
Active

---

## Key Takeaways

- This document describes a canonical contract boundary.
- Consumers should rely on Contracts rather than host internals.
- Changes must preserve compatibility or be versioned.

---

## System Context

Contracts define stable boundaries between UseCases, Services, and Data.

---

## Core Concepts

This document describes the contract intent and expected usage. Implementation details belong in code.

---

## How This Evolves Over Time

- Update when schema or interface changes are introduced.
- Note compatibility expectations when fields evolve.

---

## Common Pitfalls and Anti-Patterns

- Changing contract shapes without versioning.
- Embedding host-specific types into Contracts.

---

## When to Change This Document

- The contract or schema changes.
- New consumers depend on this boundary.

---

## Related Documents

- ../../02-architecture/system-overview.md
- ../../02-architecture/application-layer-conventions.md
- ../api/overview.md
- schema.md

---

## Appendix (Optional)

### Prior content (preserved for reference)

Source of truth: `src/Data/MGF.Data/Migrations/*`, `.github/workflows/migrate-staging.yml`, `.github/workflows/migrate-prod.yml`
Change control: Update when migration workflow or deployment gates change.
Last verified: 2025-12-30

## Summary
- Migrations live in `src/Data/MGF.Data/Migrations/`.
- Runbook steps live in [../../04-guides/how-to/db-migrations.md](../../04-guides/how-to/db-migrations.md).

---

## Metadata

Last updated: 2026-01-02  
Owner: Platform  
Review cadence: on contract change  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
