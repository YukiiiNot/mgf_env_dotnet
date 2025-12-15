# Phase 1 Core — Status

Phase 1 Core is now implemented as **EF Core migrations** in `src/MGF.Infrastructure/Migrations/`, derived from the schema-doc CSVs in `docs/db_design/schema_csv/_core/` (including `_lookup` and `_join`).

## What’s implemented

### Migrations

- `Phase1_01_Lookups`: creates all lookup/dictionary tables from `docs/db_design/schema_csv/_core/_lookup/`
- `Phase1_02_Core`: creates all core entity tables from `docs/db_design/schema_csv/_core/`
- `Phase1_03_Joins`: creates all join/association tables from `docs/db_design/schema_csv/_core/_join/`

### EF model strategy

- `src/MGF.Infrastructure/Data/SchemaDocs/SchemaDocPack.cs` parses the CSV docs.
- `src/MGF.Infrastructure/Data/SchemaDocs/SchemaDocModelBuilder.cs` builds the EF model from the docs:
  - `clients`, `people`, `projects` are mapped to CLR entities in `MGF.Domain.Entities`
  - all other tables are mapped as **property-bag** entities (`Dictionary<string, object>`) using shared-type entity names equal to the table name

### Lookup seeding

- `src/MGF.Infrastructure/Data/Seeding/LookupSeeder.cs` seeds stable lookup rows and counters idempotently (UPSERT).
- `MGF.Tools.Migrator` runs: migrations → lookup seeding.

## Notes / intentional deferrals

- **Role scope enforcement**: `project_members.role_key` correctly references global `roles.role_key`, and scopes are modeled via `role_scopes` + `role_scope_roles`, but “only project-scoped roles allowed in project_members” is not enforced at the DB level yet (would require either a redundant `scope_key` column on the join table, or a trigger). Validate in application logic for now.
- **client_contacts primary key**: the CSV does not mark `Primary=true`; Phase 1 assumes a composite PK of `(client_id, person_id)` per intended join semantics.
- **Recommended partial uniques**: some CSV notes recommend partial unique indexes (e.g., “one primary per …”). Phase 1 only implements uniqueness when it is explicitly documented in the `Constraints` field.

## Quick references

- Design docs root: `docs/db_design/schema_csv/_core/`
- Inventory: `docs/db_design/schema_csv/_notes/schema_inventory.json`
- Migration runner: `src/MGF.Tools.Migrator`
