# Container Templates

These templates describe the three storage roles used for MGFilms projects. They are "shells" only and live under `docs/templates/`.

## Dropbox (business + exchange)

Purpose: contracts, intake, client review exports, deliverables, metadata/manifests. Not an editing workspace.

Folder tree:

```
{PROJECT_CODE}_{CLIENT_NAME}_{PROJECT_NAME}/
  00_Admin/
    .mgf/
      project_metadata.json
      folder_manifest.json
    01_Briefs_Contracts/
    02_Schedules_CallSheets/
    03_References_ClientDecks/
  01_Intake/
    FromClient/
    Uploads/
    Notes_For_Intake/
  02_Exchange/
    Asset_Handoff/
    Proxy_Packages/
  03_Reviews_Client/
    Exports_For_Client/
    Client_Notes/
  04_Deliverables/
    Client_Proofs/
    Final_Masters/
    Social_Cutdowns/
  99_Archive/
```

## LucidLink (active editorial)

Purpose: Premiere Productions workspace, shared project files, and authoritative proxies. No cache folders live here.

Folder tree:

```
{PROJECT_CODE}_{CLIENT_NAME}_{PROJECT_NAME}/
  00_{PROJECT_CODE}_{PROJECT_NAME}_PRODUCTION/
  01_Edit/
    MASTER/
    WORKING/{EDITOR_INITIALS}/
    EXTERNALS/
      AE/
        MASTER/
        {EDITOR_INITIALS}/
      PS/
        MASTER/
        {EDITOR_INITIALS}/
      AI/
        MASTER/
        {EDITOR_INITIALS}/
      AUD/
        MASTER/
        {EDITOR_INITIALS}/
  02_Media/
    PROXIES/
    AUDIO/
    GRAPHICS/
  03_Renders_Internal/
    From_Premiere/
    From_AE/
```

Policy notes:
- No caches (Media Cache, previews, auto-saves) on LucidLink; caches are local-only.
- The production folder is project-scoped so Premiere does not show identical names across projects.

## NAS (cold archive)

Purpose: originals and final deliverables only. Proxies/mezzanine are regenerable and should not live here.

Folder tree:

```
{PROJECT_CODE}_{CLIENT_NAME}_{PROJECT_NAME}/
  00_ProjectInfo/
    source_manifest.json
    notes.txt
  01_Originals/
    CameraCards/
    Audio/
    Photos_Graphics_Source/
  02_Deliverables/
    Final_Masters/
  99_Legacy/ (optional)
```

Policy notes:
- Store originals and final deliverables only.
- Do not store proxies or mezzanine assets on NAS long-term.
