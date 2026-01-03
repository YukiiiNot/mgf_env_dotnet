# Business Concepts Catalog

**Title:** Business Concepts Catalog  
**Purpose:** Drift-prevention list of first-class business concepts and where they live in code today.  
**Audience:** Engineers working across Core, Application, Services, Data, and Integrations.  
**Scope:** Concepts and their current representations; not a full data dictionary or an architecture redesign.  
**Last updated:** 2026-01-02  
**Owner:** Architecture  
**Related docs:** [domain-persistence-map.md](domain-persistence-map.md), [extension-playbook.md](extension-playbook.md), [system-overview.md](system-overview.md), [project-shapes.md](project-shapes.md)

## Status legend
- **Implemented:** concept is actively used with at least one workflow/use-case and stable persistence.
- **Partially Implemented:** partial coverage (tables exist, or a workflow exists), but concept is not fully modeled across layers.
- **Planned:** tables and/or contracts may exist, but no active workflows or domain modeling yet.

## Catalog
| Concept | Status | Current representations | Source-of-truth locations | Next promotion trigger | Notes / risks |
| --- | --- | --- | --- | --- | --- |
| Project | Implemented | Domain entity; UseCases for bootstrap/delivery; Data stores + tables | `src/Core/MGF.Domain/Entities/Entities.cs`, `src/Application/MGF.UseCases/UseCases/Operations/ProjectBootstrap`, `src/Application/MGF.UseCases/UseCases/Operations/ProjectDelivery`, `src/Data/MGF.Data/Stores/ProjectBootstrap`, `src/Data/MGF.Data/Stores/Delivery`, tables: `projects`, `project_members`, `project_storage_roots` | Cross-workflow invariants that must be reused (status transitions, code rules) | Primary aggregate today; keep invariants centralized |
| Client | Partially Implemented | Domain entity; Data tables | `src/Core/MGF.Domain/Entities/Entities.cs`, tables: `clients`, `client_contacts`, `client_billing_profiles` | Client lifecycle or billing policy reused across workflows | Few workflows yet; avoid ad hoc client models outside Core |
| Person | Partially Implemented | Domain entity; Data tables | `src/Core/MGF.Domain/Entities/Entities.cs`, tables: `people`, `person_contacts`, `person_roles` | Shared identity/contact invariants (roles, contact precedence) | Contacts often duplicated; keep canonical in Data + Contracts |
| Delivery | Partially Implemented | UseCases + Data stores; no Domain aggregate yet | `src/Application/MGF.UseCases/UseCases/Operations/ProjectDelivery`, `src/Application/MGF.UseCases/UseCases/DeliveryEmail`, `src/Data/MGF.Data/Stores/Delivery`, tables: `deliverables`, `deliverable_versions` | Shared rules for stable paths, versions, and share links across workflows | Invariants live in use-cases/adapters; avoid new delivery models in hosts |
| Invoice | Planned | Data tables only | tables: `invoices`, `invoice_items`, `invoice_statuses`, `invoice_number_counters`, `invoice_integrations_square` | Introduction of invoice workflows or calculations | Avoid creating invoice models in Services/UI |
| Payment | Planned | Data tables only | tables: `payments`, `payment_methods`, `payment_statuses`, `payment_processors` | Payment workflows or reconciliation logic | Keep provider-specific logic in Integrations |
| Booking | Planned | Data tables only | tables: `bookings`, `booking_attendees`, `booking_phases`, `booking_statuses` | Booking scheduling workflows or availability rules | Avoid mixing with leads until rules are defined |
| Lead | Planned | Data tables only | tables: `leads`, `lead_stages`, `lead_outcomes`, `lead_priorities`, `lead_sources`, `lead_tags` | Lead conversion rules or pipeline automation | Treat pipeline rules as UseCases first |
| WorkItem | Planned | Data tables only | tables: `work_items`, `work_item_statuses`, `work_item_priorities`, `work_item_tags` | Task workflow or assignment rules | Avoid host-local work item models |

## Notes
- “First-class concept” does **not** mean a full Domain model today; it marks concepts that need stable ownership.
- When a concept grows shared invariants, promote it into Domain or Contracts, not into hosts.
