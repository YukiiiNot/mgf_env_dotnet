# MGF.LegacyAuditCli

> How-to for running the legacy audit tool and interpreting outputs.

---

## MetaData

**Purpose:** Provide a supported workflow for running the legacy audit tool.
**Scope:** Covers usage, profiles, outputs, and classifications. Excludes tool implementation details.
**Doc Type:** How-To
**Status:** Active
**Last Updated:** 2026-01-07

---

## TL;DR

- Run the CLI with `scan --root` to generate JSON and CSV reports.
- Use profiles to control exclusions.
- Outputs are written under `runtime/legacy_audit/outputs` by default.

---

## Main Content

Read-only NAS audit tool for legacy cleanup planning. Scans a UNC root and produces JSON and CSV reports in a local output folder.

## Run

```powershell
# Writes are gated: use --apply to write reports.
# Output defaults under runtime\legacy_audit\outputs\<root>_<timestamp>
dotnet run --project src/DevTools/MGF.LegacyAuditCli -- scan --root "\\Truenas\zan4k pool\OFFLOAD 2" --apply
```

## Profiles

- `editorial` (default): excludes common system folders (Recycle Bin, Spotlight, etc.).
- `everything`: disables default excludes.

## Examples (real NAS roots)

```powershell
# OFFLOAD 2 (default output under runtime)
dotnet run --project src/DevTools/MGF.LegacyAuditCli -- scan --root "\\Truenas\zan4k pool\OFFLOAD 2" --apply

# OFDLOAD 3 (spelling as-is)
dotnet run --project src/DevTools/MGF.LegacyAuditCli -- scan --root "\\Truenas\zan4k pool\OFDLOAD 3" --apply

# OFFLOAD 4 (empty)
dotnet run --project src/DevTools/MGF.LegacyAuditCli -- scan --root "\\Truenas\zan4k pool\OFFLOAD 4" --apply

# OFFLOAD 5 (empty)
dotnet run --project src/DevTools/MGF.LegacyAuditCli -- scan --root "\\Truenas\zan4k pool\OFFLOAD 5" --apply

# ZANA Sector 2
dotnet run --project src/DevTools/MGF.LegacyAuditCli -- scan --root "\\Truenas\zana 10tb - 01\Sector 2" --apply

# ZANA Sector 01
dotnet run --project src/DevTools/MGF.LegacyAuditCli -- scan --root "\\Truenas\zana 20tb - 00\SECTOR 01" --apply
```

## Classifications

The tool does not attempt to label personal vs business. It only surfaces structural signals:

- `project_confirmed` (marker kind=project)
- `container_confirmed` (marker kind=container)
- `project_root` (strong edit + media signals)
- `project_container` (contains multiple project roots)
- `camera_dump_subtree` (camera card structures or raw-only media)
- `template_pack` (template/course patterns)
- `cache_only` (autosave/preview-only structures)
- `unknown_needs_review` (triage bucket, not a claim)
- `empty_folder`

## Outputs

Outputs are written to `--out` when provided. If omitted, output defaults under `runtime/legacy_audit/outputs/...`.
CSV exports include `root_share`, `relative_path`, and human-readable size columns:

- `scan_report.json` (full machine-readable report)
- `scan_summary.txt` (human summary)
- `triage.csv` (sorted by priority then size)
- `projects_candidates.csv` (project_root)
- `project_containers.csv`
- `projects_confirmed.csv` (marker present)
- `camera_dumps.csv` (includes inferred project/container paths)
- `cache_folders.csv` (autosave/audio/video preview folders)
- `empty_folders.csv`
- `templates.csv`
- `top_dirs.csv`
- `top_files.csv`
- `files_by_ext.csv`
- `dup_candidates.csv` (file+folder candidates with match basis)

For folder duplicates, `group_key` is a fingerprint (stable hash of immediate child names + file sizes).

## Marker file

If `_mgf_project.tag.json` or `_MGF_PROJECT.tag.json` exists in a folder, that folder is classified as confirmed.

- `kind=project` => `project_confirmed`
- `kind=container` => `container_confirmed`

Heuristic and hierarchy scores are still computed and included in `scan_report.json` for transparency.

---

## System Context

This guide supports legacy cleanup planning by producing a consistent audit snapshot of storage roots.

---

## Core Concepts

- The audit is read-only and classification-focused.
- Reports emphasize structural signals rather than business semantics.

---

## How This Evolves Over Time

- Add profiles and classifications as audit needs expand.
- Update output formats when downstream tools change.

---

## Common Pitfalls and Anti-Patterns

- Running without `--apply` and expecting output files.
- Treating classifications as definitive without manual review.

---

## When to Change This Document

- CLI arguments, profiles, classifications, or output files change.

---

## Related Documents
- dev-guide.md
- repo-workflow.md
- system-overview.md

## Change Log
- 2026-01-07 - Reformatted to documentation standards.
