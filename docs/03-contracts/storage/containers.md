# Container Templates

Source of truth: `docs/templates/*.json`, `docs/schemas/*.schema.json`, `src/Operations/MGF.Tools.Provisioner`
Change control: Update when container folder structures or naming rules change.
Last verified: 2025-12-30


These templates describe the three storage roles used for MGFilms projects. They are "shells" only and live under `docs/templates/`.

## Dropbox (business + exchange)

Purpose: contracts, intake, client review exports, deliverables, metadata/manifests. Not an editing workspace.

Folder tree:

```
{PROJECT_CODE}_{CLIENT_NAME}_{PROJECT_NAME}/
  00_Admin/
    .mgf/
      project_metadata.json
      manifest/
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
  99_Dump/
```

## LucidLink (active editorial)

Purpose: Premiere Productions workspace, shared project files, and authoritative proxies. No cache folders live here.

Folder tree:

```
{PROJECT_CODE}_{CLIENT_NAME}_{PROJECT_NAME}/
  00_Admin/
    .mgf/
      manifest/
  00_Production_{PROJECT_CODE}/
    Premiere/
      MASTER/
      WORKING/{EDITOR_INITIALS}/
    AfterEffects/
      MASTER/
      {EDITOR_INITIALS}/
    Photoshop/
    Illustrator/
    Audition/
  01_Media/
    Proxies/
      Video/
      Audio/
    Audio/
      Music/
      VO/
      SFX/
    Graphics/
      Logos/
      LowerThirds/
      Brand/
    Fonts/
    Stills/
    Reference/
  02_Renders/
    Internal_Reviews/
    Client_Reviews/
    Final_Masters/
  03_Notes/
    Editorial_Notes/
    Client_Notes/
    Delivery_Checklists/
  99_Dump/
```

Policy notes:
- No caches (Media Cache, previews, auto-saves) on LucidLink; caches are local-only.
- The production folder is project-scoped so Premiere does not show identical names across projects.

## NAS (cold archive)

Purpose: originals and final deliverables only. Proxies/mezzanine are regenerable and should not live here.

Folder tree:

```
{PROJECT_CODE}_{CLIENT_NAME}_{PROJECT_NAME}/
  00_Admin/
    .mgf/
      source_manifest.json
      notes.txt
      manifest/
  01_Originals/
    CameraCards/
    Audio/
    Photos_Graphics_Source/
  02_Deliverables/
    Final_Masters/
  03_ProjectFiles_Snapshots/ (optional)
  99_Dump/
  99_Legacy/ (optional)
```

Policy notes:
- Store originals and final deliverables only.
- Do not store proxies or mezzanine assets on NAS long-term.
