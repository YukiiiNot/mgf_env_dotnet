# Phase 1 Core Status

Purpose  
Define architecture boundaries and responsibilities for this area.

Audience  
Engineers extending or refactoring system boundaries.

Scope  
Covers boundaries, ownership, and dependency direction. Does not include operational steps.

Status  
Needs Review. Reason: Phase 1 scope may be outdated after refactors.

---

## Key Takeaways

- This doc defines architecture boundaries for this area.
- Follow the bucket ownership rules and dependency direction.
- Use related docs when extending or refactoring.

---

## System Context

Architecture docs define bucket responsibilities and dependency direction.

---

## Core Concepts

This document explains the boundary and responsibilities for this area and how it fits into the bucket model.

---

## How This Evolves Over Time

- Update when bucket boundaries or dependency rules change.
- Add notes when a new project or workflow is introduced.

---

## Common Pitfalls and Anti-Patterns

- Putting workflow logic in hosts instead of UseCases.
- Introducing vendor logic outside Integrations.

---

## When to Change This Document

- Bucket ownership or dependency rules change.
- A new workflow impacts the described boundaries.

---

## Related Documents

- system-overview.md
- application-layer-conventions.md
- project-shapes.md
- persistence-patterns.md

---

## Appendix (Optional)

### Prior content (preserved for reference)

Phase 1 Core is now implemented as **EF Core migrations** in `src/Data/MGF.Data/Migrations/`, derived from the schema-doc CSVs in `docs/db_design/schema_csv/_core/` (including `_lookup` and `_join`).

## What's implemented

### Migrations

- `Phase1_01_Lookups`: creates all lookup/dictionary tables from `docs/db_design/schema_csv/_core/_lookup/`
- `Phase1_02_Core`: creates all core entity tables from `docs/db_design/schema_csv/_core/`
- `Phase1_03_Joins`: creates all join/association tables from `docs/db_design/schema_csv/_core/_join/`

### EF model strategy

- Runtime model is built in `src/Data/MGF.Data/Data/AppDbContextModelBuilder.cs` and used by `src/Data/MGF.Data/Data/AppDbContext.cs`.
- Migration snapshot lives in `src/Data/MGF.Data/Migrations/AppDbContextModelSnapshot.cs` and tracks the current model for EF.
- Executable schema changes live in `src/Data/MGF.Data/Migrations/*.cs`.
- Schema CSVs in `docs/db_design/schema_csv/_core/` are design-time docs; they are not parsed at runtime.

### Lookup seeding

- `src/Data/MGF.Data/Data/Seeding/LookupSeeder.cs` seeds stable lookup rows and counters idempotently (UPSERT).
- `MGF.DataMigrator` runs: migrations → lookup seeding.

## Notes / intentional deferrals

- **Role scope enforcement**: `project_members.role_key` correctly references global `roles.role_key`, and scopes are modeled via `role_scopes` + `role_scope_roles`, but only project-scoped roles allowed in project_members is not enforced at the DB level yet (would require either a redundant `scope_key` column on the join table, or a trigger). Validate in application logic for now.
- **client_contacts primary key**: the CSV does not mark `Primary=true`; Phase 1 assumes a composite PK of `(client_id, person_id)` per intended join semantics.
- **Recommended partial uniques**: some CSV notes recommend partial unique indexes (e.g., “one primary per …”). Phase 1 only implements uniqueness when it is explicitly documented in the `Constraints` field.

## Quick references

- Design docs root: `docs/db_design/schema_csv/_core/`
- Inventory: `docs/db_design/schema_csv/_notes/schema_inventory.json`
- Migration runner: `src/Data/MGF.DataMigrator`

---

## Metadata

Last updated: 2026-01-02  
Owner: Architecture  
Review cadence: on major architecture change  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
