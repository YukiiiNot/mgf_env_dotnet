# Domain and Persistence Map

---

## MetaData

**Purpose:** Define architecture boundaries and responsibilities for this area.
**Scope:** Covers boundaries, ownership, and dependency direction. Does not include operational steps.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-06
**Review Cadence:** on major architecture change

---

## TL;DR

- This doc defines architecture boundaries for this area.
- Follow the bucket ownership rules and dependency direction.
- Use related docs when extending or refactoring.

---

## Main Content

### Why models differ by layer
- **Domain (Core):** holds reusable invariants and behavior. Only add entities/value objects when rules are shared across workflows.
- **Contracts (Core):** stable boundaries (interfaces + DTOs) used by UseCases and hosts.
- **UseCases (Application):** orchestration and workflow logic; only depends on Contracts and Domain.
- **Data:** EF models, migrations, and SQL stores; holds persistence-specific shapes and mappings.

### Table categorization
We categorize tables so we do **not** create one Domain entity per table.

- **A: Core business tables** (candidates for Domain aggregates or persisted state)
- **B: Relationship/join tables** (links between aggregates)
- **C: Lookup/reference tables** (enums, statuses, types)
- **D: Operational tables** (jobs, counters, logs, audit/event)
- **E: Integration-specific persistence** (Square, storage provider metadata)

### Current table inventory by category
Source of truth: `src/Data/MGF.Data/Migrations/AppDbContextModelSnapshot.cs`

### A) Core business tables
- `bookings`
- `client_billing_profiles`
- `clients`
- `deliverable_versions`
- `deliverables`
- `invoice_items`
- `invoices`
- `leads`
- `payments`
- `people`
- `person_contacts`
- `projects`
- `work_items`

### B) Relationship/join tables
- `booking_attendees`
- `client_contacts`
- `lead_tags`
- `person_known_hosts`
- `person_permissions`
- `person_roles`
- `project_members`
- `role_scope_roles`
- `work_item_tags`

### C) Lookup/reference tables
- `activity_ops`
- `activity_priorities`
- `activity_statuses`
- `activity_topics`
- `booking_phases`
- `booking_statuses`
- `client_statuses`
- `client_types`
- `currencies`
- `data_profiles`
- `deliverable_statuses`
- `delivery_methods`
- `host_keys`
- `integration_sync_statuses`
- `invoice_statuses`
- `job_priorities`
- `job_statuses`
- `job_types`
- `lead_outcomes`
- `lead_priorities`
- `lead_sources`
- `lead_stages`
- `path_anchors`
- `path_settings`
- `path_templates`
- `path_types`
- `payment_methods`
- `payment_processors`
- `payment_statuses`
- `permissions`
- `person_statuses`
- `project_phases`
- `project_priorities`
- `project_statuses`
- `role_scopes`
- `roles`
- `service_packages`
- `slug_scopes`
- `storage_providers`
- `storage_root_contracts`
- `tags`
- `work_item_priorities`
- `work_item_statuses`

### D) Operational tables
- `activity_acknowledgements`
- `activity_log`
- `invoice_number_counters`
- `jobs`
- `project_code_counters`
- `slug_reservations`

### E) Integration-specific persistence
- `client_integrations_square`
- `invoice_integrations_square`
- `person_calendar_sync_settings`
- `project_storage_roots`
- `square_customer_creation_sources`
- `square_reconcile_cursors`
- `square_sync_review_queue`
- `square_webhook_events`

### Expected .NET representation by category
| Category | Expected representation | Ownership |
| --- | --- | --- |
| A: Core business | Domain entity/value object if shared invariants exist; otherwise persisted state + Contracts DTOs | Domain (rules), Contracts (boundary), Data (persistence) |
| B: Join | EF-only relationship or store method; no Domain entity unless it carries rules | Data; Contracts only if a use-case needs it |
| C: Lookup | Enum/value object in Domain only if behavior is shared; otherwise Data-only with Contracts DTOs | Domain (rare), Data (default) |
| D: Operational | Data store methods or EF persistence; UseCases orchestrate | Data + UseCases |
| E: Integration-specific | Data store methods; Integrations provide IO; UseCases coordinate | Data + Integrations + UseCases |

### Anti-drift rules
- UI/Services/Operations **must not** invent concept models; use Contracts or Domain.
- UseCases **must not** reference Data or EF directly.
- Integrations is vendor-only (third-party APIs). Generic system capabilities belong in Platform or Services adapters.

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
