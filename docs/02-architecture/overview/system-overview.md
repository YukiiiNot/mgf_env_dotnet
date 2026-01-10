# System Overview

---

## MetaData

**Purpose:** Define architecture boundaries and responsibilities for this area.
**Scope:** Covers boundaries, ownership, and dependency direction. Does not include operational steps.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-10
**Review Cadence:** on major architecture change

---

## TL;DR

- This doc defines architecture boundaries for this area.
- Follow the bucket ownership rules and dependency direction.
- Use related docs when extending or refactoring.

---

## Main Content

This repo hosts MGF's internal API, worker, desktop app, and supporting tools for storage provisioning, migrations, and delivery workflows.

### Components
- `src/Services/MGF.Api` - internal API entrypoint for apps and integrations.
- `src/Services/MGF.Worker` - background job processor for provisioning, delivery, and integrations.
- `src/Ui/MGF.Desktop.Shared` - shared WPF views/resources for the desktop ops console.
- `src/Ui/MGF.DevConsole.Desktop` - desktop ops console host (early stage).
- `src/Ui/MGF.Website` - web UI host (stub).
- `src/DevTools/*` and `src/Operations/*` - CLIs for migrations, provisioning, delivery, and audits.
- `src/Platform/MGF.Email` - email composition/registry used by Worker and ops tools (templates copied by hosts).
- `src/Platform/MGF.FolderProvisioning` - provisioning engine (template planning/execution) with replaceable policy rules.
- `src/Platform/MGF.Storage` - storage/local filesystem adapters (RootIntegrity executor).
- `src/Data/MGF.Data` - shared data access, configuration, and EF model.
- `src/Integrations/MGF.Integrations.Email.*` - provider-specific email senders (Gmail, SMTP).
- `src/Integrations/MGF.Integrations.*` - external API adapters (Dropbox, etc.).
- `src/Application/MGF.UseCases` - use-case boundary for business workflows.

### Runtime flow
Worker/API/CLI -> UseCases -> Contracts -> Data/Integrations (CLIs call UseCases; do not reference service hosts).
Project bootstrap: Worker -> `IBootstrapProjectUseCase` -> Contracts store -> Data + `IProjectBootstrapProvisioningGateway` (Services adapter: `src/Services/MGF.Worker/Adapters/Storage/ProjectBootstrap/`).
Square webhooks: API -> `IIngestSquareWebhookUseCase` -> Contracts store -> Data.
People list: API -> `IListPeopleUseCase` -> Contracts store -> Data.

### DevConsole Jobs surface
- Endpoint: `GET /api/jobs` (list) with cursorCreatedAt + cursorJobId pagination.
- Default since: server UTC now - 24h, based on created_at (job creation time).
- Ordering: created_at desc, job_id desc for stable pagination.
- Detail: `GET /api/jobs/{jobId}` (payload lives in detail, not list).

### DevConsole Projects surface
- Endpoint: `GET /api/projects` (list) with cursorCreatedAt + cursorProjectId pagination.
- Default limit: 200 (bounded list, newest-first).
- Default since: server UTC now - 24h, based on created_at (project creation time).
- Ordering: created_at desc, project_id desc for stable pagination.
- Detail: `GET /api/projects/{projectId}` (operator-safe fields only).
- Jobs list uses created_at for the "last 24 hours" window (server UTC).
- Jobs payload is detail-only and opt-in in the DevConsole UI (truncated at 50 KB).
- /api/* requires X-MGF-API-KEY; X-MGF-Operator is optional for audit strings.

### Use-case boundary (MGF.UseCases)
MGF.UseCases is the boundary project for business use-cases and workflows; all business writes flow through use-cases.

Examples that belong here:
- CreateProject
- CreateDeliveryVersion
- SendDeliveryEmail

Does not belong here: DbContext, Dropbox SDK, SMTP client.

### Related docs
- Workflow overview: workflows.md
- Provisioning engine: provisioning.md
- Project shapes: project-shapes.md
- Persistence patterns: persistence-patterns.md
- Runbooks: repo-workflow.md
- Contracts index: 03-contracts

---

## System Context

Architecture docs define bucket responsibilities and dependency direction.

---

## Core Concepts

This document explains the boundary and responsibilities for this area and how it fits into the bucket model.

---

## How This Evolves Over Time

- Update when bucket boundaries or dependency rules change.
- Add notes when a new project or workflow is introduced.

---

## Common Pitfalls and Anti-Patterns

- Putting workflow logic in hosts instead of UseCases.
- Introducing vendor logic outside Integrations.

---

## When to Change This Document

- Bucket ownership or dependency rules change.
- A new workflow impacts the described boundaries.

---

## Related Documents

- system-overview.md
- application-layer-conventions.md
- project-shapes.md
- persistence-patterns.md

## Change Log
- Date format: YYYY-MM-DD (see doc-enumerations.md)
- 2026-01-06 - Reformatted into the new documentation standards format; content preserved.
- 2026-01-02 - Reformatted to the documentation template.
