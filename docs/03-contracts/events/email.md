# Email

Purpose  
Define the contract boundary and expectations for this area.

Audience  
Engineers building or consuming contracts and integrations.

Scope  
Covers contract intent and boundary expectations. Does not describe host wiring.

Status  
Active

---

## Key Takeaways

- This document describes a canonical contract boundary.
- Consumers should rely on Contracts rather than host internals.
- Changes must preserve compatibility or be versioned.

---

## System Context

Contracts define stable boundaries between UseCases, Services, and Data.

---

## Core Concepts

This document describes the contract intent and expected usage. Implementation details belong in code.

---

## How This Evolves Over Time

- Update when schema or interface changes are introduced.
- Note compatibility expectations when fields evolve.

---

## Common Pitfalls and Anti-Patterns

- Changing contract shapes without versioning.
- Embedding host-specific types into Contracts.

---

## When to Change This Document

- The contract or schema changes.
- New consumers depend on this boundary.

---

## Related Documents

- ../../02-architecture/system-overview.md
- ../../02-architecture/application-layer-conventions.md
- ../api/overview.md
- ../database/schema.md

---

## Appendix (Optional)

### Prior content (preserved for reference)

MGF Email Contract (Internal)
=============================

Source of truth: `src/Platform/MGF.Email/**`, `src/Integrations/MGF.Integrations.Email.*`, `src/Operations/MGF.ProjectBootstrapCli`, `tests/MGF.Worker.Tests/EmailSnapshots`
Change control: Update when email metadata contract, allowlist, or template versions change.
Last verified: 2025-12-30


Purpose
-------
This document defines the minimum email contract that must remain compatible.
It exists to prevent accidental breakage while reorganizing email code.

Canonical metadata fields (must remain compatible)
--------------------------------------------------
Delivery email metadata is stored in project metadata:

projects.metadata.delivery.current.lastEmail:
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

projects.metadata.delivery.runs[].email:
- status, provider, fromAddress, to, subject, sentAtUtc, providerMessageId,
  error, templateVersion, replyTo (same shape as lastEmail)

FromAddress allowlist policy (must remain)
------------------------------------------
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

Multipart requirement (must remain)
-----------------------------------
Every outgoing email must be multipart/alternative:
- text/plain part
- text/html part

The plain text fallback must always be included.

Delivery-ready required content (must remain)
---------------------------------------------
Delivery-ready emails must include at minimum:
- Stable Dropbox share link (Final folder)
- Current delivery version label (vN)
- Retention message (3 months, with date)
- File list (at least filenames)

Ergonomics enforcement (design contract for refactor)
-----------------------------------------------------
After the email subsystem refactor:
- All sends go through one entry point: EmailService.SendAsync(kind, context, overrides?).
- Delivery code must not build HTML/text strings directly.
- Templates live under src/Platform/MGF.Email/Composition/Templates and must remain stable unless explicitly changed.

MGF Email Kit (templates + theme)
---------------------------------
Templates live under:
- src/Platform/MGF.Email/Composition/Templates/
- src/Platform/MGF.Email/Composition/Templates/partials/
- src/Platform/MGF.Email/Composition/Templates/theme.json

The delivery-ready HTML template is assembled from partials:
layout_start -> header_block -> headline_block -> rule -> CTA -> link -> rule -> details -> rule -> files -> footer -> layout_end.

Theme tokens (theme.json) drive typography, spacing, rules, and button styling.
All values are inline-safe and email-client friendly; missing tokens fall back to
EmailTheme.Default.

Creating a new email template
-----------------------------
1) Add templates:
   - Templates/<kind>.html
   - Templates/<kind>.txt
2) Compose from partials in Templates/partials/.
3) Add a composer that maps the context to EmailMessage.
4) Add a fixture (optional) for preview and tests.
5) Add a snapshot test (EmailSnapshots/*.html) to lock rendering.

Preview (no sending)
--------------------
Use the ProjectBootstrap CLI to render HTML/text without sending:
    dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture basic --out .\runtime\email_preview\basic
    dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture large_files --out .\runtime\email_preview\large
    dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture no_logo --out .\runtime\email_preview\nologo
    dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture long_url --out .\runtime\email_preview\long_url

Fixtures live under Templates/fixtures/*.json.

Tests
-----
Snapshot tests compare rendered HTML with:
    tests/MGF.Worker.Tests/EmailSnapshots/*.html

Run tests:
    dotnet test -c Release tests/MGF.Worker.Tests/MGF.Worker.Tests.csproj

Updating snapshots
------------------
To regenerate a fixture snapshot, run email-preview with --writeSnapshots and --snapshotOut:
    dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture basic --out .\runtime\email_preview\basic --writeSnapshots --snapshotOut .\tests\MGF.Worker.Tests\EmailSnapshots
    dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture large_files --out .\runtime\email_preview\large --writeSnapshots --snapshotOut .\tests\MGF.Worker.Tests\EmailSnapshots
    dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture no_logo --out .\runtime\email_preview\nologo --writeSnapshots --snapshotOut .\tests\MGF.Worker.Tests\EmailSnapshots
    dotnet run --project src/Operations/MGF.ProjectBootstrapCli -- email-preview --fixture long_url --out .\runtime\email_preview\long_url --writeSnapshots --snapshotOut .\tests\MGF.Worker.Tests\EmailSnapshots

---

## Metadata

Last updated: 2026-01-02  
Owner: Platform  
Review cadence: on contract change  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
