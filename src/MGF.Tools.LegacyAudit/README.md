# MGF.Tools.LegacyAudit

Read-only NAS audit tool for legacy cleanup planning. Scans a UNC root and produces JSON + CSV reports in a local output folder.

## Run

```powershell
$env:MGF_ENV = "Dev"
dotnet run --project src/MGF.Tools.LegacyAudit -- scan --root "\\Truenas\zan4k pool\OFFLOAD 2" --out "C:\mgf_audit_out\OFFLOAD_2_2025-01-05"
```

## Profiles

- `editorial` (default): excludes common system folders (Recycle Bin, Spotlight, etc.).
- `everything`: disables default excludes.

## Examples (real NAS roots)

```powershell
# OFFLOAD 2
dotnet run --project src/MGF.Tools.LegacyAudit -- scan --root "\\Truenas\zan4k pool\OFFLOAD 2" --out "C:\mgf_audit_out\OFFLOAD_2_2025-01-05"

# OFDLOAD 3 (spelling as-is)
dotnet run --project src/MGF.Tools.LegacyAudit -- scan --root "\\Truenas\zan4k pool\OFDLOAD 3" --out "C:\mgf_audit_out\OFDLOAD_3_2025-01-05"

# OFFLOAD 4 (empty)
dotnet run --project src/MGF.Tools.LegacyAudit -- scan --root "\\Truenas\zan4k pool\OFFLOAD 4" --out "C:\mgf_audit_out\OFFLOAD_4_2025-01-05"

# OFFLOAD 5 (empty)
dotnet run --project src/MGF.Tools.LegacyAudit -- scan --root "\\Truenas\zan4k pool\OFFLOAD 5" --out "C:\mgf_audit_out\OFFLOAD_5_2025-01-05"

# ZANA Sector 2
dotnet run --project src/MGF.Tools.LegacyAudit -- scan --root "\\Truenas\zana 10tb - 01\Sector 2" --out "C:\mgf_audit_out\ZANA10_Sector2_2025-01-05"

# ZANA Sector 01
dotnet run --project src/MGF.Tools.LegacyAudit -- scan --root "\\Truenas\zana 20tb - 00\SECTOR 01" --out "C:\mgf_audit_out\ZANA20_Sector01_2025-01-05"
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

All outputs are written to the `--out` folder. CSVs include `root_share`, `relative_path`, and human-readable size columns:

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
