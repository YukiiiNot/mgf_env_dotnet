# Schema CSV System

Purpose  
Provide stable reference information for this area.

Audience  
Engineers needing canonical reference information.

Scope  
Covers stable reference material. Does not provide step-by-step procedures.

Status  
Active

---

## Key Takeaways

- This page is a reference; it is not a step-by-step guide.
- Treat listed conventions as the canonical baseline.
- Update this doc when conventions change.

---

## System Context

Reference docs capture stable conventions and definitions used across the repo.

---

## Core Concepts

This document records the canonical reference information for the repo.

---

## How This Evolves Over Time

- Update as conventions evolve.
- Remove deprecated guidance once fully superseded.

---

## Common Pitfalls and Anti-Patterns

- Treating reference content as a runtime API.
- Leaving outdated conventions unmarked.

---

## When to Change This Document

- Conventions change or become obsolete.
- New reference material is added or removed.

---

## Related Documents

- ../../documentation-standards.md
- ../../naming-rules.md
- ../../style-guide.md

---

## Appendix (Optional)

### Prior content (preserved for reference)

These CSV files are database design/specification artifacts for the MGF relational schema (Supabase/Postgres). They are intended for design review, schema cleanup, and documentation.

EF Core migrations are the executable source of truth; these CSVs are design-time documentation and a review surface.

References:
- [docs/04-guides/how-to/db-migrations.md](../../04-guides/how-to/db-migrations.md)
- [docs/03-contracts/database/schema.md](../../03-contracts/database/schema.md)

## Folder meanings

- `_core`: Active schema documentation (v1). This is what `schema_inventory.json` is built from (recursive).
  - `_core/_lookup`: Lookup/dictionary tables.
  - `_core/_join`: Join/association tables.
- `_hold`: v2 candidates / staging. Not referenced by `_core`.
- `_deprecated`: Archived schemas retained for traceability. Not referenced by `_core`.
- `_notes`: Generated artifacts and audit notes (inventory, changelog, ORM readiness reports).

## Conventions

- Table docs: `*_schema_documentation.csv`
- `FKTarget` values use `table.column` and should reference tables within `_core`.

---

## Metadata

Last updated: 2026-01-02  
Owner: Data  
Review cadence: semiannually  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
