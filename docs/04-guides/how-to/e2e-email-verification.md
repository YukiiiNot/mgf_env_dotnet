# E2E Email Verification (Dev)

Purpose  
Provide a supported, repeatable workflow for this task.

Audience  
Engineers performing this task or workflow.

Scope  
Covers the supported workflow and prerequisites. Does not define low-level implementation.

Status  
Active

---

## Key Takeaways

- This guide explains the supported workflow for this task.
- Use linked runbooks and contracts for deeper detail.
- Avoid ad hoc or undocumented shortcuts.

---

## System Context

This guide sits between onboarding and runbooks and references the canonical architecture.

---

## Core Concepts

This guide describes the supported workflow and where the authoritative sources live. Detailed steps are in the appendix.

---

## How This Evolves Over Time

- Expand as new supported workflows are added.
- Retire sections when they are superseded by new tooling.

---

## Common Pitfalls and Anti-Patterns

- Using ad hoc shortcuts instead of documented workflows.
- Duplicating guidance that already exists elsewhere.

---

## When to Change This Document

- Supported workflow or tooling changes.
- New prerequisites are required.

---

## Related Documents

- ../../01-onboarding/dev-guide.md
- ../../05-runbooks/repo-workflow.md
- ../../02-architecture/system-overview.md

---

## Appendix (Optional)

### Prior content (preserved for reference)

This procedure verifies delivery email end-to-end without loosening the recipient guardrails. The send path always
uses canonical recipients from the database.

## Prereqs
- Dev configuration loaded (`MGF_ENV`, `MGF_DB_MODE`, `MGF_CONFIG_DIR`).
- Email enabled: `Integrations:Email:Enabled=true`.
- Preview provider (recommended for dev): `Integrations:Email:Provider=preview`.
  - Optional output path: `Integrations:Email:Preview:OutputDir`.
  - Default output path: `runtime/email_preview` under the current working directory.
- Storage roots configured for delivery/bootstrap:
  - `Storage:LucidLinkRoot` must point at the dev LucidLink root.
  - `Storage:DropboxRoot` must point at the dev Dropbox root if you need share links.
- Dropbox API auth available (access token or refresh token) if you need a stable share link.
  - If your local Dropbox root is a subfolder of the account root, set:
    `Integrations:Dropbox:UseApiRootFolder=true` and `Integrations:Dropbox:ApiRootFolder=<path in Dropbox>`.

## Steps
1) Seed a fixture project and canonical recipient:
```powershell
dotnet run -c Release --project src\DevTools\MGF.ProjectBootstrapDevCli -- seed-e2e-delivery-email --email <addr>
```
This prints `project_id`, canonical `recipients`, and whether `stableShareUrl` exists. If the stable link is missing,
the command prints the exact delivery command to run.

If you already have a project with a stable share link, pass it explicitly:
```powershell
dotnet run -c Release --project src\DevTools\MGF.ProjectBootstrapDevCli -- seed-e2e-delivery-email --email <addr> --projectId <project_id>
```

2) If required, run the printed delivery command to create the stable share link.

If delivery fails with `No deliverable files found in Final_Masters`, seed at least one file with an allowed extension
(e.g., `.mp4`, `.mov`, `.pdf`) before re-running delivery:
```powershell
dotnet run -c Release --project src\DevTools\MGF.ProjectBootstrapDevCli -- seed-deliverables `
  --projectId <project_id> `
  --file <local_path_with_allowed_extension> `
  --target final-masters `
  --testMode false `
  --overwrite
```

3) Enqueue the delivery email job using the same recipient:
```powershell
dotnet run -c Release --project src\Operations\MGF.ProjectBootstrapCli -- delivery-email `
  --projectId <project_id> `
  --to <addr> `
  --from deliveries@mgfilms.pro `
  --replyTo info@mgfilms.pro
```

4) Process the job:
```powershell
dotnet run -c Release --project src\Services\MGF.Worker -- --maxJobs 1
```

5) Verify:
- `delivery.current.lastEmail.status` updates on the project.
- `delivery.current.lastEmail.provider` is `preview` if the preview provider is selected.
- Preview artifacts exist in the output directory (`message.txt`, `message.html`, `message.json`).

## Notes
- The delivery-email use-case ignores payload overrides; it validates that observed recipients match the canonical
  recipients from the database.

---

## Metadata

Last updated: 2026-01-02  
Owner: Engineering  
Review cadence: quarterly  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
