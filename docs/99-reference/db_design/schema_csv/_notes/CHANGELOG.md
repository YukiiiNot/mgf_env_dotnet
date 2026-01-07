# Schema CSV Cleanup Changelog

Generated: 2025-12-14 17:22:35Z

## Backup
- Created backup: `C:\dev\mgf_env_legacy\mgf_config\spreadsheets\data_db_design_sheets\csv_backup_before_cleanup`

## Summary
- Roles: unified under `roles` + generated `role_scopes` / `role_scope_roles` and rewired all role FKs.
- payment_methods: verified single `payment_methods` table (no duplicates found).
- Tags: verified single `tags` dictionary + domain join tables (no duplicate tag dictionaries found).
- Invoice numbers (v1): no ledger/status tables present; standardized invoice number CHECK format across docs.
- integration_sync_statuses: verified single `integration_sync_statuses` table.
- Derivable duplication removed (v1 defaults): dropped `payments.project_id`, `payments.client_id`, `deliverables.client_id`.
- Polymorphic typing helper: added `entity_type_key` to `activity_log` and `jobs`.

## Modified Files
- `activity_log_schema_documentation.csv`
- `booking_attendees_schema_documentation.csv`
- `client_contacts_schema_documentation.csv`
- `deliverables_schema_documentation.csv`
- `invoices_add_invoice_number_field_schema_note.csv`
- `invoices_schema_documentation.csv`
- `jobs_schema_documentation.csv`
- `payments_schema_documentation.csv`
- `project_members_schema_documentation.csv`

## Deprecated Schemas
- `_deprecated\booking_attendee_roles_schema_documentation.csv` (merged into `roles`)
- `_deprecated\client_contact_roles_schema_documentation.csv` (merged into `roles`)
- `_deprecated\project_roles_schema_documentation.csv` (merged into `roles`)

## Generated Schemas
- `_generated\role_scope_roles_schema_documentation.csv`
- `_generated\role_scopes_schema_documentation.csv`

## Notes / Decisions
- Invoice number format chosen: `MGF-INV-YY-XXXXXX` (6 digits), reflected in `invoices` and schema note.
- Removed denormalized columns rather than marking deprecated, per default v1 policy.

## 2025-12-15 — Docs re-org
- Reorganized active `_core` schema docs into subfolders:
  - `_core/_lookup` for lookup/dictionary tables
  - `_core/_join` for join/association tables
- Updated `_notes/schema_inventory.json` `csv_path` entries to match the new layout.
- (none)

## Resolved
- 2025-12-14 17:38:48Z: Resolved missing lookup table `person_statuses` by restoring `person_statuses_schema_documentation.csv`.

## Restore: person_statuses
- 2025-12-14 17:38:48Z: Restored `person_statuses_schema_documentation.csv` from backup into active CSV directory to satisfy `people.status_key` FK.

## 2025-12-14 19:07:53Z — booking_attendees
- Table: booking_attendees
- Change: Replaced composite PK (booking_id, person_id) with surrogate PK booking_attendee_id
- Reason: ORM friendliness + future lifecycle/payload expansion
- Natural uniqueness preserved via UNIQUE (booking_id, person_id)

## 2025-12-14 19:29:14Z — v2 type lookups (hold)
- Added v2 candidate lookup tables in `_hold`: `booking_types`, `project_types`, `deliverable_types`.
- Not referenced by any active `_core` schemas; inventory not rebuilt.

---

## Metadata

Last updated: 2026-01-02  
Owner: Data  
Review cadence: semiannually  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
