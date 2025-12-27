# Folder Templates

## Purpose

- Domain Root templates define top-level storage taxonomy contracts (verify for drift).
- Project Container templates define per-project scaffolding.

## Rules

- Top-level folders must use numeric prefixes only (e.g., `00_Admin`, `01_PreProduction`).
- `.mgf` metadata folder exists only under `00_Admin` in the Dropbox template.
- `.prproj` filenames are stable:
  - MASTER: `{PROJECT_CODE}_{PROJECT_NAME}_MASTER.prproj`
  - Editor: `{PROJECT_CODE}_{PROJECT_NAME}_{EDITOR_INITIALS}.prproj`
- Exports/renders use versioned filenames (e.g., `..._EXPORT_v###.mp4`).

## Files

Domain Root templates:
- `domain_dropbox_root.json`
- `domain_lucidlink_root.json`
- `domain_nas_root.json`

Project Container templates:
- `dropbox_project_container.json`
- `lucidlink_production_container.json`
- `nas_archive_container.json`

Schemas live in `../schemas/`

## How to run the Provisioner (defaults)

Validate a template (uses `.\docs\schemas\mgf.folderTemplate.schema.json` by default):

```powershell
dotnet run --project .\src\MGF.Tools.Provisioner -- validate --template .\docs\templates\lucidlink_production_container.json
```

Plan a folder tree (defaults output to `.\runtime\provisioner_runs`):

```powershell
dotnet run --project .\src\MGF.Tools.Provisioner -- plan --template .\docs\templates\lucidlink_production_container.json --projectCode MGF25-0001 --projectName Example --clientName Client --editors ER
```

Apply + verify (also defaults to `.\runtime\provisioner_runs`):

```powershell
dotnet run --project .\src\MGF.Tools.Provisioner -- apply --template .\docs\templates\lucidlink_production_container.json --projectCode MGF25-0002 --projectName ExampleApply --clientName Client --editors ER
dotnet run --project .\src\MGF.Tools.Provisioner -- verify --template .\docs\templates\lucidlink_production_container.json --projectCode MGF25-0002 --projectName ExampleApply --clientName Client --editors ER
```
