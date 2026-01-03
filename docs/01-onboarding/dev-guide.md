# Developer Guide

Purpose  
Help contributors onboard with durable guidance and links.

Audience  
New contributors and engineers onboarding to the repo.

Scope  
Covers orientation and links. Does not replace runbooks or detailed guides.

Status  
Active

---

## Key Takeaways

- This doc helps new contributors get oriented quickly.
- Follow linked guides for environment setup and workflows.
- When in doubt, follow the documented process.

---

## System Context

Onboarding docs orient new contributors to the repo and its workflows.

---

## Core Concepts

This document helps contributors understand the repo and where to find deeper guidance.

---

## How This Evolves Over Time

- Update when scope or responsibilities change.
- Add clarifications when recurring questions appear.

---

## Common Pitfalls and Anti-Patterns

- Letting scope creep beyond the stated boundaries.
- Creating duplicate sources of truth.

---

## When to Change This Document

- Scope or responsibilities change.
- New related docs are added.

---

## Related Documents

- ../00-index.md
- ../02-architecture/system-overview.md
- dev-guide.md

---

## Appendix (Optional)

### Prior content (preserved for reference)

Purpose  
Teach contributors how to work inside the system day-to-day: how to add new functionality without violating boundaries, how to reason about workflows, and how to keep changes safe and maintainable as the system expands.

Audience  
Developers building features, workflows, UI, integrations, and operational tooling.

Scope  
Covers development principles, boundaries, and where changes go. Does not replace runbooks (operational “how to run”) and does not fully enumerate every subsystem (architecture docs do that).

Key takeaways
- The system is workflow-driven: UseCases orchestrate; Hosts dispatch; Data persists; Integrations talk to vendors.
- We preserve consistency by enforcing dependency direction and contract surfaces.
- “How the business works” belongs in Domain/UseCases; “how data is stored” belongs in Data; “how we talk to vendors” belongs in Integrations.
- If you’re about to add a feature, start by deciding which bucket owns it.

## How the system is intended to evolve

As MGF expands, we avoid drift by keeping a stable separation of concerns:
- Domain describes business concepts and invariants (small and intentional, not a table mirror).
- Contracts defines cross-bucket interfaces and shared models (the handshake).
- UseCases orchestrate workflows using Contracts interfaces (persistence-ignorant).
- Data implements persistence and stores (EF + SQL) behind Contracts.
- Services (API/Worker) and Operations (CLI) are adapters that call UseCases.
- Integrations is vendor-only client code (Square/Dropbox/Gmail/etc.).
- Platform is reusable technical infrastructure (cross-cutting, non-vendor, non-business).

For the authoritative shapes and dependency direction, see:  
- ../02-architecture/project-shapes.md  
- ../02-architecture/extension-playbook.md  

## Working agreements (what we optimize for)

- Guardrails over cleverness: prefer tests, contracts, and explicit seams.
- Explicit workflows: jobs and status transitions are explicit, not “hidden side effects.”
- Idempotency: job processing should tolerate retries safely.
- Traceability: when behavior matters, make it observable (logs + stored metadata + docs).

## Where to add things

New workflow / business capability:
- Define the Contracts surface first (models + interfaces)
- Implement orchestration in UseCases
- Implement persistence in Data stores behind Contracts
- Implement IO adapters in Services adapters (filesystem/storage/email sending)
- Keep Hosts thin: they parse input, call UseCases, and report results

Common placement map:
- UseCases: src/Application/MGF.UseCases/UseCases/<Area>/<UseCaseName>/
- Contracts: src/Core/MGF.Contracts/Abstractions/<Area>/
- Data stores: src/Data/MGF.Data/Stores/<Area>/
- Vendor client code: src/Integrations/MGF.Integrations.<Vendor>/
- Host wiring: src/Services/MGF.Api and src/Services/MGF.Worker
- CLI adapters: src/Operations/* (calls UseCases, never Data directly)
- Runbooks: docs/05-runbooks/

## Integrations (vendor-only)

Add new vendor integrations under:
- src/Integrations/MGF.Integrations.<Provider>/

Rules of thumb:
- Integrations contains HTTP clients, auth/token handling, provider models, and provider-specific failure translation.
- Integrations does not contain workflow orchestration.
- Prefer Contracts abstractions for anything the rest of the system calls.

## Email (conceptual overview)

Email spans multiple buckets:
- Platform: composition and templates (what we send and how it renders)
- Integrations: providers (how it’s sent via Gmail/SMTP/etc.)
- Contracts: abstractions for senders/composers and shared models
- UseCases: when/why an email is sent (workflow orchestration)
- Services: wiring and runtime selection

For operational steps and verification, use the runbooks and verification doc:
- ../02-architecture/testing-and-verification.md

## Folder provisioning (conceptual overview)

MGF.FolderProvisioning is a folder topology engine used by workflows to:
- plan/apply/verify folder structures across storage containers
- enforce folder structure rules via policy

It is not a “generic provisioning system,” it’s explicitly about folder trees across storage providers.

Authoritative detail:
- ../02-architecture/provisioning.md

## Testing philosophy

- Unit tests for pure logic (planners, guards, models)
- Contract tests for “must-not-drift” invariants (stable delivery paths, allowlists, naming rules)
- Avoid destructive DB tests unless explicitly opted-in and isolated

Related docs
- ../01-onboarding/getting-started.md
- ../02-architecture/project-shapes.md
- ../02-architecture/domain-persistence-map.md
- ../02-architecture/extension-playbook.md
- ../05-runbooks/ (runbooks index)

Last updated: 2026-01-02  
Owner: Repo maintainers / Infra owner  
Status: Draft

---

## Metadata

Last updated: 2026-01-02  
Owner: Engineering Enablement  
Review cadence: quarterly  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
