# E2E Email Verification (Dev)

> How-to for validating the delivery email flow end-to-end in Dev.

---

## MetaData

**Purpose:** Provide a supported workflow to verify delivery email end-to-end in Dev.
**Scope:** Covers prerequisites, steps, and verification checks. Excludes implementation details.
**Doc Type:** How-To
**Status:** Active
**Last Updated:** 2026-01-07

---

## TL;DR

- Seed a fixture project and recipients, then create a stable share link if needed.
- Enqueue and process the delivery email job.
- Verify project metadata and preview artifacts.

---

## Main Content

This procedure verifies delivery email end-to-end without loosening recipient guardrails. The send path always
uses canonical recipients from the database.

## Prereqs
- Dev configuration loaded (MGF_ENV, MGF_DB_MODE, MGF_CONFIG_DIR).
- Email enabled: Integrations:Email:Enabled=true.
- Preview provider (recommended for dev): Integrations:Email:Provider=preview.
  - Optional output path: Integrations:Email:Preview:OutputDir.
  - Default output path: runtime/email_preview under the current working directory.
- Storage roots configured for delivery/bootstrap:
  - Storage:LucidLinkRoot must point at the dev LucidLink root.
  - Storage:DropboxRoot must point at the dev Dropbox root if you need share links.
- Dropbox API auth available (access token or refresh token) if you need a stable share link.
  - If your local Dropbox root is a subfolder of the account root, set:
    Integrations:Dropbox:UseApiRootFolder=true and Integrations:Dropbox:ApiRootFolder=<path in Dropbox>.

## Steps
1) Seed a fixture project and canonical recipient:
```powershell
dotnet run -c Release --project src\DevTools\MGF.ProjectBootstrapDevCli -- seed-e2e-delivery-email --email <addr>
```
This prints project_id, canonical recipients, and whether stableShareUrl exists. If the stable link is missing,
the command prints the exact delivery command to run.

If you already have a project with a stable share link, pass it explicitly:
```powershell
dotnet run -c Release --project src\DevTools\MGF.ProjectBootstrapDevCli -- seed-e2e-delivery-email --email <addr> --projectId <project_id>
```

2) If required, run the printed delivery command to create the stable share link.

If delivery fails with No deliverable files found in Final_Masters, seed at least one file with an allowed extension
(e.g., .mp4, .mov, .pdf) before re-running delivery:
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
- delivery.current.lastEmail.status updates on the project.
- delivery.current.lastEmail.provider is preview if the preview provider is selected.
- Preview artifacts exist in the output directory (message.txt, message.html, message.json).

## Notes
- The delivery-email use-case ignores payload overrides; it validates that observed recipients match the canonical
  recipients from the database.

---

## System Context

This guide validates the delivery email workflow across use-cases, worker jobs, and email integrations.

---

## Core Concepts

- Canonical recipients are read from the database and must not be overridden.
- Stable share links are required for delivery-ready emails.

---

## How This Evolves Over Time

- Update when delivery email workflows, config keys, or job payloads change.
- Add new verification steps when new failure modes appear.

---

## Common Pitfalls and Anti-Patterns

- Sending without a stable share link.
- Forgetting to enable the preview provider in Dev.

---

## When to Change This Document

- The delivery email workflow or prerequisites change.
- New verification outputs are added.

---

## Related Documents
- delivery.md
- jobs.md
- env-vars.md
- dev-secrets.md

## Change Log
- 2026-01-07 - Reformatted to documentation standards.
