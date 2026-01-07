# Email

> Contract for delivery email metadata, template rules, and sending requirements.

---

## MetaData

**Purpose:** Define the minimum email contract that must remain compatible across refactors.
**Scope:** Covers metadata fields, allowlist rules, and template expectations. Excludes provider implementation details.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-07

---

## TL;DR

- Delivery email metadata is stored under project metadata and must remain compatible.
- From addresses are allowlisted; every email must be multipart.
- Templates and snapshots are the contract for rendering.

---

## Main Content

Source of truth: `src/Platform/MGF.Email/**`, `src/Integrations/MGF.Integrations.Email.*`, `src/Operations/MGF.ProjectBootstrapCli`, `tests/MGF.Worker.Tests/EmailSnapshots`

## Contract scope

This document defines the minimum email contract that must remain compatible to prevent accidental breakage.

## Canonical metadata fields (must remain compatible)

Delivery email metadata is stored in project metadata:

`projects.metadata.delivery.current.lastEmail`:
- status: sent|failed|skipped
- provider: smtp|gmail
- fromAddress: string (must be allowlisted)
- to: string[]
- subject: string
- sentAtUtc: ISO string or null
- providerMessageId: string or null
- error: string or null
- templateVersion: string (e.g., v1-html)
- replyTo: string or null

`projects.metadata.delivery.runs[].email`:
- status, provider, fromAddress, to, subject, sentAtUtc, providerMessageId,
  error, templateVersion, replyTo (same shape as lastEmail)

## From address allowlist (must remain)

Only these From addresses are allowed:
- admin@mgfilms.pro
- info@mgfilms.pro
- contact@mgfilms.pro
- billing@mgfilms.pro
- support@mgfilms.pro
- bookings@mgfilms.pro
- deliveries@mgfilms.pro
- ermano.cayard@mgfilms.pro
- creative@mgfilms.pro
- ops@mgfilms.pro
- cayard.ermano@mgfilms.pro
- ermano@mgfilms.pro
- martin.price@mgfilms.pro
- ceo@mgfilms.pro
- price.martin@mgfilms.pro
- dex@mgfilms.pro
- martin@mgfilms.pro

Any other From address must fail fast with a clear error.

## Multipart requirement (must remain)

Every outgoing email must be multipart/alternative:
- text/plain part
- text/html part

The plain text fallback must always be included.

## Delivery-ready required content (must remain)

Delivery-ready emails must include at minimum:
- Stable Dropbox share link (Final folder)
- Current delivery version label (vN)
- Retention message (3 months, with date)
- File list (at least filenames)

## Ergonomics enforcement (design contract for refactor)

After the email subsystem refactor:
- All sends go through one entry point: `EmailService.SendAsync(kind, context, overrides?)`.
- Delivery code must not build HTML or text strings directly.
- Templates live under `src/Platform/MGF.Email/Composition/Templates` and must remain stable unless explicitly changed.

## MGF Email Kit (templates + theme)

Templates live under:
- `src/Platform/MGF.Email/Composition/Templates/`
- `src/Platform/MGF.Email/Composition/Templates/partials/`
- `src/Platform/MGF.Email/Composition/Templates/theme.json`

The delivery-ready HTML template is assembled from partials:
`layout_start -> header_block -> headline_block -> rule -> CTA -> link -> rule -> details -> rule -> files -> footer -> layout_end`.

Theme tokens (`theme.json`) drive typography, spacing, rules, and button styling.

## Creating a new email template

1) Add templates:
   - Templates/<kind>.html
   - Templates/<kind>.txt
2) Compose from partials in Templates/partials/.
3) Add a composer that maps the context to EmailMessage.
4) Add a fixture (optional) for preview and tests.
5) Add a snapshot test (EmailSnapshots/*.html) to lock rendering.

## Preview (no sending)

Use the ProjectBootstrap CLI to render HTML/text without sending:

```powershell
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture basic --out runtime\email_preview\basic
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture large_files --out runtime\email_preview\large
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture no_logo --out runtime\email_preview\nologo
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture long_url --out runtime\email_preview\long_url
```

Fixtures live under Templates/fixtures/*.json.

## Tests

Snapshot tests compare rendered HTML with:
`tests/MGF.Worker.Tests/EmailSnapshots/*.html`

Run tests:

```powershell
dotnet test -c Release tests/MGF.Worker.Tests/MGF.Worker.Tests.csproj
```

## Updating snapshots

To regenerate a fixture snapshot, run email-preview with `--writeSnapshots` and `--snapshotOut`:

```powershell
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture basic --out runtime\email_preview\basic --writeSnapshots --snapshotOut tests\MGF.Worker.Tests\EmailSnapshots
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture large_files --out runtime\email_preview\large --writeSnapshots --snapshotOut tests\MGF.Worker.Tests\EmailSnapshots
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture no_logo --out runtime\email_preview\nologo --writeSnapshots --snapshotOut tests\MGF.Worker.Tests\EmailSnapshots
dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture long_url --out runtime\email_preview\long_url --writeSnapshots --snapshotOut tests\MGF.Worker.Tests\EmailSnapshots
```

---

## System Context

Email contracts sit at the boundary between delivery workflows, integrations, and template composition.

---

## Core Concepts

- Delivery email metadata is part of the project record and must remain compatible.
- From-address allowlists and multipart rules guard compliance and deliverability.
- Templates and snapshots are the rendering contract for delivery emails.

---

## How This Evolves Over Time

- Update when metadata fields, allowlist entries, or template versions change.
- Document any contract shifts in template composition or send flow.

---

## Common Pitfalls and Anti-Patterns

- Sending from a non-allowlisted address.
- Rendering HTML without a plain-text part.
- Changing templates without updating snapshots.

---

## When to Change This Document

- Metadata fields, allowlist policy, or template rules change.
- Email preview or snapshot workflows change.

---

## Related Documents
- integrations.md
- delivery.md
- e2e-email-verification.md

## Change Log
- 2026-01-07 - Reformatted to documentation standards.
