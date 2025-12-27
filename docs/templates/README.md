# Folder Templates

## Purpose

- Legacy templates reflect the earlier NAS + Dropbox workflow.
- Container templates reflect the current storage roles (Dropbox business/exchange, LucidLink active edit, NAS archive).

## Rules

- Top-level folders must use numeric prefixes only (e.g., `00_Admin`, `01_PreProduction`).
- `.mgf` metadata folder exists only under `00_Admin` in the Dropbox template.
- `.prproj` filenames are stable:
  - MASTER: `{PROJECT_CODE}_{PROJECT_NAME}_MASTER.prproj`
  - Editor: `{PROJECT_CODE}_{PROJECT_NAME}_{EDITOR_INITIALS}.prproj`
- Exports/renders use versioned filenames (e.g., `..._EXPORT_v###.mp4`).

## Files

Legacy templates:
- `nas_production_template.json`
- `dropbox_project_template.json`

Container templates:
- `dropbox_project_container.json`
- `lucidlink_production_container.json`
- `nas_archive_container.json`

Schemas live in `../schemas/`
