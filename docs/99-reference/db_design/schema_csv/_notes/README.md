# Schema CSV System


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
