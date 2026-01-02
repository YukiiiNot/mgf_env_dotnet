# Jobs Contract

Source of truth: `src/Services/MGF.Worker/**`, `src/Data/MGF.Data/Migrations/*`, `src/Data/MGF.Data/Data/Seeding/LookupSeeder.cs`
Change control: Update when job schema, status transitions, or payload shape changes.
Last verified: 2025-12-30

## Scope
- Jobs are persisted in `public.jobs` and processed by `src/Services/MGF.Worker`.
- Job types and payload shapes must remain compatible across releases.

## Job schema (public.jobs)
- Source: `src/Data/MGF.Data/Migrations/20251215075215_Phase1_02_Core.cs`, `src/Data/MGF.Data/Migrations/AppDbContextModelSnapshot.cs`, `src/Data/MGF.Data/Data/AppDbContextModelBuilder.cs`.
- Key columns: `job_id`, `job_type_key`, `status_key`, `priority_key`, `payload` (jsonb), `run_after`, `locked_by`, `locked_until`, `attempt_count`, `max_attempts`, `started_at`, `finished_at`.
- Status values (seeded): `queued`, `running`, `succeeded`, `failed`, `cancelled` (`src/Data/MGF.Data/Data/Seeding/LookupSeeder.cs`).

## Job types (seeded)
Source: `src/Data/MGF.Data/Data/Seeding/LookupSeeder.cs`

- `dropbox.create_project_structure`
- `project.bootstrap`
- `project.archive`
- `project.delivery`
- `project.delivery_email`
- `domain.root_integrity`
- `notion.sync_booking`
- `square.reconcile.payments`
- `square.payment.upsert`
- `square.webhook_event.process`

Note: `notion.sync_booking` is seeded but currently has no handler in `JobWorker`.

## Worker-handled job types
Source: `src/Services/MGF.Worker/JobWorker.cs` (unknown `job_type_key` throws)

- `dropbox.create_project_structure`
- `project.bootstrap`
- `project.archive`
- `project.delivery`
- `project.delivery_email`
- `domain.root_integrity`
- `square.reconcile.payments`
- `square.payment.upsert`
- `square.webhook_event.process`

## Payload shapes (JSONB)
- `dropbox.create_project_structure`: `{ projectId?, clientId?, templateKey? }` (read directly in `JobWorker`).
- `project.bootstrap`: `ProjectBootstrapPayload` in `src/Core/MGF.Contracts/Abstractions/ProjectBootstrap/ProjectBootstrapModels.cs`.
- `project.archive`: `ProjectArchivePayload` in `src/Services/MGF.Worker/ProjectArchive/ProjectArchiveModels.cs`.
- `project.delivery`: `ProjectDeliveryPayload` in `src/Core/MGF.Contracts/Abstractions/ProjectDelivery/ProjectDeliveryModels.cs`.
- `project.delivery_email`: `ProjectDeliveryEmailPayload` in `src/Core/MGF.Contracts/Abstractions/ProjectDelivery/ProjectDeliveryModels.cs`.
- `domain.root_integrity`: `RootIntegrityPayload` in `src/Core/MGF.Contracts/Abstractions/RootIntegrity/RootIntegrityModels.cs`.
- `square.webhook_event.process`: `{ square_event_id }` required; parsed by `JobWorker.TryExtractSquareEventId`.
- `square.payment.upsert`: `{ square_payment_id }` required; parsed by `JobWorker.ExtractSquarePaymentId`.
- `square.reconcile.payments`: no payload fields required; behavior is driven by config and reconcile cursor state.

## Change control notes
- Update this doc when `job_types` seed list changes, handler dispatch list changes, payload model records change, or `public.jobs` schema changes.

## References
- Worker dispatch: `src/Services/MGF.Worker/JobWorker.cs`
- Payload models: `src/Core/MGF.Contracts/Abstractions/ProjectBootstrap/ProjectBootstrapModels.cs`, `src/Services/MGF.Worker/ProjectArchive/ProjectArchiveModels.cs`, `src/Core/MGF.Contracts/Abstractions/ProjectDelivery/ProjectDeliveryModels.cs`, `src/Core/MGF.Contracts/Abstractions/RootIntegrity/RootIntegrityModels.cs`
- Job types seed: `src/Data/MGF.Data/Data/Seeding/LookupSeeder.cs`

