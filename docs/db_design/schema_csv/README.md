# Schema CSV System

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
