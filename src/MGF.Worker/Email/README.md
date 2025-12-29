MGF Email Contract (Internal)
=============================

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
- deliveries@mgfilms.pro
- info@mgfilms.pro

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
- Phase 1 refactor should NOT move templates or introduce a template engine.
