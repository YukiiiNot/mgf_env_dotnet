# Folder Templates

## Purpose

- NAS template is the authoritative editing workspace for Adobe Premiere Productions.
- Dropbox template is for intake, delivery, references, and metadata only (not editing).

## Rules

- Top-level folders must use numeric prefixes only (e.g., `00_Admin`, `01_PreProduction`).
- `.mgf` metadata folder exists only under `00_Admin` in the Dropbox template.
- `.prproj` filenames are stable:
  - MASTER: `{PROJECT_CODE}_{PROJECT_NAME}_MASTER.prproj`
  - Editor: `{PROJECT_CODE}_{PROJECT_NAME}_{EDITOR_INITIALS}.prproj`
- Exports/renders use versioned filenames (e.g., `..._EXPORT_v###.mp4`).

## Files

- `nas_production_template.json`
- `dropbox_project_template.json`
- Schemas live in `../schemas/`
