# Folder Templates

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

- ../documentation-standards.md
- ../naming-rules.md
- ../style-guide.md

---

## Appendix (Optional)

### Prior content (preserved for reference)

## Purpose

- Domain Root templates define top-level storage taxonomy contracts (verify for drift).
- Project Container templates define per-project scaffolding.

Templates are versioned infrastructure. Schema validation + tests are mandatory. See `../03-contracts/storage/infra-contracts.md`.

## Rules

- Top-level folders must use numeric prefixes only (e.g., `00_Admin`, `01_PreProduction`).
- `.mgf` metadata folder exists only under `00_Admin` (all templates) and must contain `manifest/`.
- `.prproj` filenames are stable:
  - MASTER: `{PROJECT_CODE}_{PROJECT_NAME}_MASTER.prproj`
  - Editor: `{PROJECT_CODE}_{PROJECT_NAME}_{EDITOR_INITIALS}.prproj`
- Exports/renders use versioned filenames (e.g., `..._EXPORT_v###.mp4`).
- Delivery containers use a stable `01_Deliverables\Final` folder; versions live under `Final\vN` and `delivery_manifest.json` lists file paths relative to the version folder.

## Files

Domain Root templates:
- `domain_dropbox_root.json`
- `domain_lucidlink_root.json`
- `domain_nas_root.json`

Project Container templates:
- `dropbox_project_container.json`
- `dropbox_delivery_container.json`
- `lucidlink_production_container.json`
- `nas_archive_container.json`

Templates live in `../../artifacts/templates/`

Schemas live in `../../artifacts/schemas/`

## How to run the Provisioner (defaults)

Validate a template (uses `.\artifacts\schemas\mgf.folderTemplate.schema.json` by default):

```powershell
dotnet run --project .\src\Operations\MGF.ProvisionerCli -- validate --template .\artifacts\templates\lucidlink_production_container.json
```

Plan a folder tree (defaults output to `.\runtime\provisioner_runs`):

```powershell
dotnet run --project .\src\Operations\MGF.ProvisionerCli -- plan --template .\artifacts\templates\lucidlink_production_container.json --projectCode MGF25-0001 --projectName Example --clientName Client --editors ER
```

Apply + verify (also defaults to `.\runtime\provisioner_runs`):

```powershell
dotnet run --project .\src\Operations\MGF.ProvisionerCli -- apply --template .\artifacts\templates\lucidlink_production_container.json --projectCode MGF25-0002 --projectName ExampleApply --clientName Client --editors ER
dotnet run --project .\src\Operations\MGF.ProvisionerCli -- verify --template .\artifacts\templates\lucidlink_production_container.json --projectCode MGF25-0002 --projectName ExampleApply --clientName Client --editors ER
```

---

## Metadata

Last updated: 2026-01-02  
Owner: Documentation  
Review cadence: semiannually  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
