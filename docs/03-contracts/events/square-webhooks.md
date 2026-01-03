# Square Webhooks

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

Source of truth: `src/Services/MGF.Api/Controllers/SquareWebhooksController.cs`, `src/Integrations/MGF.Integrations.Square/**`, `src/Data/MGF.Data/Data/SquareWebhookEvent.cs`, `src/Data/MGF.Data/Migrations/20251216074837_Phase1_05_SquareWebhookEvents.cs`
Change control: Update when signature verification, payload fields, or persistence behavior changes.
Last verified: 2025-12-30

## Endpoint
- `POST /webhooks/square`
- Max payload size: 262,144 bytes.

## Signature verification
- Header: `x-square-hmacsha256-signature` (legacy: `x-square-signature`).
- Config keys:
  - `Square:WebhookSignatureKey`
  - `Square:WebhookNotificationUrl` (or `Integrations:Square:WebhookNotificationUrl`)

## Persistence and jobs
- Payload stored in `public.square_webhook_events`.
- Job enqueued: `square.webhook_event.process`.

---

## Metadata

Last updated: 2026-01-02  
Owner: Platform  
Review cadence: on contract change  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
