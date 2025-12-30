# Square Webhooks

Source of truth: `src/MGF.Api/Controllers/SquareWebhooksController.cs`, `src/MGF.Infrastructure/Data/SquareWebhookEvent.cs`, `src/MGF.Infrastructure/Migrations/20251216074837_Phase1_05_SquareWebhookEvents.cs`
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
